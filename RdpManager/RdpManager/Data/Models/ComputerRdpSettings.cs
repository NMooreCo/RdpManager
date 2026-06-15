namespace RdpManager.Data.Models
{
    /// <summary>
    /// Per-computer RDP connection settings
    /// </summary>
    public class ComputerRdpSettings
    {
        /// <summary>
        /// Reference to the computer entry
        /// </summary>
        public long ComputerId { get; set; }

        /// <summary>
        /// Screen mode: 1=Windowed, 2=Fullscreen
        /// </summary>
        public int ScreenMode { get; set; } = 2;

        /// <summary>
        /// Color depth: 8, 15, 16, 24, 32
        /// </summary>
        public int ColorDepth { get; set; } = 32;

        /// <summary>
        /// Audio mode: 0=PlayOnThisComputer, 1=PlayOnRemoteComputer, 2=DoNotPlay
        /// </summary>
        public int AudioMode { get; set; } = 0;

        /// <summary>
        /// Enable clipboard sharing
        /// </summary>
        public bool EnableClipboard { get; set; } = true;

        /// <summary>
        /// Enable drive redirection
        /// </summary>
        public bool EnableDrives { get; set; } = false;

        /// <summary>
        /// Custom resolution in format "1920x1080" (null for auto)
        /// </summary>
        public string? CustomResolution { get; set; }

        /// <summary>
        /// Enable compression
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Enable desktop composition (Aero)
        /// </summary>
        public bool EnableDesktopComposition { get; set; } = true;

        /// <summary>
        /// Enable font smoothing
        /// </summary>
        public bool EnableFontSmoothing { get; set; } = true;
    }
}
