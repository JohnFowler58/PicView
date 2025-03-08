param (
    [Parameter()]
    [string]$Platform,
    
    [Parameter()]
    [string]$outputPath,
	
    [Parameter()]
    [string]$appVersion
)
# Define the core project path relative to the script's location
$coreProjectPath = Join-Path -Path $PSScriptRoot -ChildPath "..\src\PicView.Core\PicView.Core.csproj"

# Load the .csproj file as XML
[xml]$coreCsproj = Get-Content $coreProjectPath

# Define the package reference to replace
$packageRefX64 = "Magick.NET-Q8-x64"
$packageRefArm64 = "Magick.NET-Q8-arm64"

# Find the Magick.NET package reference and update it based on the platform
$packageNodes = $coreCsproj.Project.ItemGroup.PackageReference | Where-Object { $_.Include -eq $packageRefX64 -or $_.Include -eq $packageRefArm64 }
if ($packageNodes) {
    foreach ($packageNode in $packageNodes) {
        if ($Platform -eq "arm64") {
            $packageNode.Include = $packageRefArm64
        } else {
            $packageNode.Include = $packageRefX64
        }
    }
}

# Save the updated .csproj file
$coreCsproj.Save($coreProjectPath)

# Define the project path for the actual build target
$avaloniaProjectPath = Join-Path -Path $PSScriptRoot -ChildPath "../src/PicView.Avalonia.MacOS/PicView.Avalonia.MacOS.csproj"

# Create temporary build output directory
$tempBuildPath = Join-Path -Path $outputPath -ChildPath "temp"
New-Item -ItemType Directory -Force -Path $tempBuildPath

# Run dotnet publish for the Avalonia project
dotnet publish $avaloniaProjectPath `
    --runtime "osx-$Platform" `
    --self-contained true `
    --configuration Release `
    -p:PublishSingleFile=false `
    --output $tempBuildPath

# Create .app bundle structure
$appBundlePath = Join-Path -Path $outputPath -ChildPath "PicView.app"
$contentsPath = Join-Path -Path $appBundlePath -ChildPath "Contents"
$macOSPath = Join-Path -Path $contentsPath -ChildPath "MacOS"
$resourcesPath = Join-Path -Path $contentsPath -ChildPath "Resources"

# Create directory structure
New-Item -ItemType Directory -Force -Path $macOSPath
New-Item -ItemType Directory -Force -Path $resourcesPath

# Create Info.plist
$infoPlistPath = Join-Path -Path $contentsPath -ChildPath "Info.plist"  # Add this line to specify the correct path
$infoPlistContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>PicView</string>
    <key>CFBundleDisplayName</key>
    <string>PicView</string>
    <key>CFBundleIdentifier</key>
    <string>com.ruben2776.picview</string>
    <key>CFBundleVersion</key>
    <string>${appVersion}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>PicView</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>CFBundleShortVersionString</key>
    <string>${appVersion}</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSArchitecturePriority</key>
    <array>
        <string>$Platform</string>
    </array>
    <key>CFBundleSupportedPlatforms</key>
    <array>
        <string>MacOSX</string>
    </array>
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
	<key>LSApplicationCategoryType</key>
    <string>public.app-category.graphics-design</string>
</dict>
</plist>
"@

# Save Info.plist with UTF-8 encoding without BOM
$utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($infoPlistPath, $infoPlistContent, $utf8NoBomEncoding)

# Copy build output to MacOS directory
Copy-Item -Path "$tempBuildPath/*" -Destination $macOSPath -Recurse

# Copy icon if it exists
$iconSource = Join-Path -Path $PSScriptRoot -ChildPath "../src/PicView.Avalonia.MacOS/Assets/AppIcon.icns"
if (Test-Path $iconSource) {
    Copy-Item -Path $iconSource -Destination $resourcesPath
}

# Find and ensure Magick.Native is properly copied
$magickNativeFile = if ($Platform -eq "arm64") { "Magick.Native-Q8-arm64.dylib" } else { "Magick.Native-Q8-x64.dylib" }

# Check for Magick.Native in the temp build output
$magickNativePath = Get-ChildItem -Path $tempBuildPath -Filter $magickNativeFile -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName

# If found in temp build path, make sure it's copied to MacOS folder and check permissions
if ($magickNativePath) {
    Write-Host "Found Magick.Native at $magickNativePath, ensuring it's in MacOS directory"
    Copy-Item -Path $magickNativePath -Destination $macOSPath -Force
} else {
    # Try to find it elsewhere in the repository
    $magickNativePath = Get-ChildItem -Path $PSScriptRoot -Filter $magickNativeFile -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    
    if ($magickNativePath) {
        Write-Host "Found Magick.Native at $magickNativePath, copying to MacOS directory"
        Copy-Item -Path $magickNativePath -Destination $macOSPath -Force
    } else {
        Write-Warning "Could not find $magickNativeFile anywhere! App may not function correctly."
        
        # Last resort: try NuGet cache
        $nugetCachePath = if ($env:HOME) { Join-Path $env:HOME ".nuget" } else { Join-Path $env:USERPROFILE ".nuget" }
        if (Test-Path $nugetCachePath) {
            $magickNativePaths = Get-ChildItem -Path $nugetCachePath -Filter $magickNativeFile -Recurse -ErrorAction SilentlyContinue
            if ($magickNativePaths) {
                $latestMagickNative = $magickNativePaths | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                Write-Host "Found Magick.Native in NuGet cache at $($latestMagickNative.FullName), copying to MacOS directory"
                Copy-Item -Path $latestMagickNative.FullName -Destination $macOSPath -Force
            }
        }
    }
}

# After copying, verify the file exists in the MacOS directory
if (Test-Path (Join-Path $macOSPath $magickNativeFile)) {
    Write-Host "$magickNativeFile successfully copied to MacOS directory"
} else {
    Write-Warning "$magickNativeFile not found in MacOS directory after copy attempt"
}

# Remove PDB files
Get-ChildItem -Path $macOSPath -Filter "*.pdb" -Recurse | Remove-Item -Force

# Remove temporary build directory
Remove-Item -Path $tempBuildPath -Recurse -Force

# Set proper permissions for the entire .app bundle
if ($IsLinux -or $IsMacOS) {
    # Set executable permissions on all binaries and dylibs
    Get-ChildItem -Path $macOSPath -Recurse | ForEach-Object {
        if ($_.Extension -in @('.dylib', '') -or $_.Name -eq 'PicView.Avalonia.MacOS') {
            chmod +x $_.FullName
        }
    }
    
    # Specifically check for Magick.Native and ensure it's executable
    $magickNativeInMacOS = Join-Path -Path $macOSPath -ChildPath $magickNativeFile
    if (Test-Path $magickNativeInMacOS) {
        Write-Host "Setting executable permission on $magickNativeInMacOS"
        chmod +x $magickNativeInMacOS
    }
    
    # Set proper ownership and permissions for the entire .app bundle
    chmod -R 755 $appBundlePath
}