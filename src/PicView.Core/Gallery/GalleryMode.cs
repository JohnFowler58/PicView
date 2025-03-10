namespace PicView.Core.Gallery;
    /// <summary>
    /// Used for determining animation to play when switching between gallery modes
    /// </summary>
    public enum GalleryMode
    {
        FullToBottom,
        FullToClosed,
        BottomToFull,
        BottomToClosed,
        ClosedToFull,
        ClosedToBottom,
        /// <summary>
        /// Used when the gallery should be closed
        /// </summary>
        Closed,
        /// <summary>
        /// Used when the gallery should be opened, with no animation. 
        /// </summary>
        BottomNoAnimation
    }
