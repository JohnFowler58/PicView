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

# Define the package reference to replace based on the platform
if ($Platform -eq "arm64") {
    $packageRef = "Magick.NET-Q8-OpenMP-arm64"
} else {
    $packageRef = "Magick.NET-Q8-OpenMP-x64"
}

# Find the Magick.NET package reference and update it
$packageNodes = $coreCsproj.Project.ItemGroup.PackageReference | Where-Object { $_.Include -like "Magick.NET-Q8*" }
if ($packageNodes) {
    foreach ($packageNode in $packageNodes) {
        $packageNode.Include = $packageRef
    }
    
    # Save the updated .csproj file
    $coreCsproj.Save($coreProjectPath)
    Write-Host "Updated Magick.NET package reference to $packageRef"
} else {
    Write-Host "Warning: No Magick.NET package reference found to update"
}

# Define the project path for the actual build target
$avaloniaProjectPath = Join-Path -Path $PSScriptRoot -ChildPath "../src/PicView.Avalonia.MacOS/PicView.Avalonia.MacOS.csproj"

# Create temporary build output directory
$tempBuildPath = Join-Path -Path $outputPath -ChildPath "temp"
New-Item -ItemType Directory -Force -Path $tempBuildPath

# Run dotnet restore to ensure we have the updated packages
Write-Host "Restoring packages for $avaloniaProjectPath..."
dotnet restore $avaloniaProjectPath

# Run dotnet publish for the Avalonia project
Write-Host "Publishing project for osx-$Platform..."
dotnet publish $avaloniaProjectPath `
    --runtime "osx-$Platform" `
    --self-contained true `
    --configuration Release `
    -p:PublishSingleFile=false `
    -p:MagickCopyNativeMacOS=true `
    --output $tempBuildPath

# Create .app bundle structure
$appBundlePath = Join-Path -Path $outputPath -ChildPath "PicView.app"
$contentsPath = Join-Path -Path $appBundlePath -ChildPath "Contents"
$macOSPath = Join-Path -Path $contentsPath -ChildPath "MacOS"
$resourcesPath = Join-Path -Path $contentsPath -ChildPath "Resources"
$frameworksPath = Join-Path -Path $contentsPath -ChildPath "Frameworks"

# Create directory structure
New-Item -ItemType Directory -Force -Path $macOSPath
New-Item -ItemType Directory -Force -Path $resourcesPath
New-Item -ItemType Directory -Force -Path $frameworksPath

# Create Info.plist
$infoPlistPath = Join-Path -Path $contentsPath -ChildPath "Info.plist"
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
Write-Host "Copying build output to .app structure..."
Copy-Item -Path "$tempBuildPath/*" -Destination $macOSPath -Recurse

# Find and copy Magick.NET dylibs to the Frameworks directory
Write-Host "Looking for Magick.NET dylibs..."
$dylibs = Get-ChildItem -Path $tempBuildPath -Filter "*.dylib" -Recurse
if ($dylibs) {
    Write-Host "Found $($dylibs.Count) dylibs, copying to Frameworks directory"
    foreach ($dylib in $dylibs) {
        Copy-Item -Path $dylib.FullName -Destination $frameworksPath -Force
    }
} else {
    Write-Host "Warning: No dylibs found in the build output"
    
    # Find Magick.NET package in the NuGet cache and copy dylibs if available
    $nugetPackagesDir = if ($env:NUGET_PACKAGES) { 
        $env:NUGET_PACKAGES 
    } else {
        Join-Path -Path $HOME -ChildPath ".nuget/packages"
    }
    
    # Look for the Magick.NET package
    $magickPackageDir = Join-Path -Path $nugetPackagesDir -ChildPath $packageRef
    
    if (Test-Path $magickPackageDir) {
        Write-Host "Found Magick.NET package directory: $magickPackageDir"
        $newestVersion = (Get-ChildItem $magickPackageDir | Sort-Object -Property Name -Descending)[0].Name
        $runtimesDir = Join-Path -Path $magickPackageDir -ChildPath "$newestVersion/runtimes/osx-$Platform/native"
        
        if (Test-Path $runtimesDir) {
            Write-Host "Copying dylibs from package cache: $runtimesDir"
            $packageDylibs = Get-ChildItem -Path $runtimesDir -Filter "*.dylib"
            foreach ($dylib in $packageDylibs) {
                Copy-Item -Path $dylib.FullName -Destination $frameworksPath -Force
                # Also copy to the MacOS directory as a fallback
                Copy-Item -Path $dylib.FullName -Destination $macOSPath -Force
            }
        } else {
            Write-Host "Couldn't find expected native runtimes in NuGet package: $runtimesDir"
        }
    } else {
        Write-Host "Couldn't find Magick.NET package in NuGet cache"
    }
}

# Copy icon if it exists
$iconSource = Join-Path -Path $PSScriptRoot -ChildPath "../src/PicView.Avalonia.MacOS/Assets/AppIcon.icns"
if (Test-Path $iconSource) {
    Copy-Item -Path $iconSource -Destination $resourcesPath
}

# Remove PDB files
Get-ChildItem -Path $macOSPath -Filter "*.pdb" -Recurse | Remove-Item -Force

# Create a script to fix library paths in the main executable
$fixDylibPath = Join-Path -Path $macOSPath -ChildPath "fix_dylibs.sh"
$fixDylibScript = @"
#!/bin/bash
cd "\$(dirname "\$0")"

# Process PicView executable
EXECUTABLE="./PicView"

# Process each dylib in the Frameworks directory
for dylib in ../Frameworks/*.dylib; do
    # Get just the filename
    dylib_name=\$(basename \$dylib)
    echo "Processing \$dylib_name"
    
    # For executable
    install_name_tool -change "@rpath/\$dylib_name" "@executable_path/../Frameworks/\$dylib_name" \$EXECUTABLE
    
    # For each dylib that might depend on other dylibs
    for target_dylib in ../Frameworks/*.dylib; do
        install_name_tool -change "@rpath/\$dylib_name" "@executable_path/../Frameworks/\$dylib_name" \$target_dylib
    done
done

echo "Library paths fixed successfully"
"@

# Save fix_dylibs script
$utf8NoBomEncoding = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($fixDylibPath, $fixDylibScript, $utf8NoBomEncoding)

# Remove temporary build directory
Remove-Item -Path $tempBuildPath -Recurse -Force

# Set proper permissions for the entire .app bundle
if ($IsLinux -or $IsMacOS) {
    # Make the fix_dylibs.sh script executable
    chmod +x $fixDylibPath
    
    # Run the fix_dylibs script if we're on macOS
    if ($IsMacOS) {
        Write-Host "Running fix_dylibs.sh script"
        & $fixDylibPath
    }
    
    # Set executable permissions on all binaries and dylibs
    Get-ChildItem -Path $macOSPath -Recurse | ForEach-Object {
        if ($_.Extension -in @('.dylib', '') -or $_.Name -eq 'PicView') {
            chmod +x $_.FullName
        }
    }
    
    # Set executable permissions on dylibs in Frameworks directory
    Get-ChildItem -Path $frameworksPath -Filter "*.dylib" | ForEach-Object {
        chmod +x $_.FullName
    }
    
    # Set proper ownership and permissions for the entire .app bundle
    chmod -R 755 $appBundlePath
}

Write-Host "Build completed successfully: $appBundlePath"