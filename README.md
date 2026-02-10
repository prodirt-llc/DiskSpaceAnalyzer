# Disk Space Analyzer - MSP Tool

A Windows Forms application for visualizing disk space usage with a SpaceMonger/SpaceSniffer-style treemap interface.

## Features

- **Treemap Visualization** - Color-coded boxes sized by disk usage (red = largest, green = smallest)
- **Click to Drill Down** - Navigate into folders by clicking
- **Filter Support** - Hide specific paths to see the rest clearly
- **Built-in Scanner** - Scan drives directly or import JSON data
- **Export Functionality** - Export analysis data to JSON
- **System Context Compatible** - Runs perfectly in ConnectWise Automate backstage mode (SYSTEM account)
- **Lightweight** - Framework-dependent build creates a ~500KB-1MB executable

## Requirements

- Windows 7 or later (Windows 10/11/Server recommended)
- .NET Framework 4.8 (pre-installed on Windows 10 1903+ and Windows Server 2019+)
- Visual Studio 2017 or later (for building)

## Building the Application

### Option 1: Using Visual Studio (Easiest)

1. Open `DiskSpaceAnalyzer.sln` in Visual Studio
2. Select **Release** configuration (top toolbar)
3. Build > Build Solution (or press Ctrl+Shift+B)
4. Your executable will be in: `DiskSpaceAnalyzer\bin\Release\DiskSpaceAnalyzer.exe`

### Option 2: Using MSBuild (Command Line)

```cmd
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" DiskSpaceAnalyzer.sln /p:Configuration=Release
```

Adjust the path based on your Visual Studio installation location.

### Option 3: Using Developer Command Prompt

1. Open "Developer Command Prompt for VS"
2. Navigate to the project folder
3. Run:
```cmd
msbuild DiskSpaceAnalyzer.sln /p:Configuration=Release
```

## Deployment

The built executable (`DiskSpaceAnalyzer.exe`) is framework-dependent and requires .NET Framework 4.8 on the target machine. Since Windows 10 (1903+) and Windows Server 2019+ include this by default, you can simply:

1. Copy `DiskSpaceAnalyzer.exe` to your destination
2. Run it - no installation needed!

**For older Windows versions:** If .NET Framework 4.8 is not installed, users can download it from Microsoft (it's a small one-time install).

## Usage

### Basic Usage

1. Launch `DiskSpaceAnalyzer.exe`
2. Click **Scan Drive** and select a drive or folder
3. Wait for the scan to complete
4. Navigate by clicking on folders in the treemap
5. Use **Back** and **Home** buttons to navigate

### Filter Feature

1. Click the **Filter** button to show the filter bar
2. Type a path fragment (e.g., "Users" to hide C:\Users\)
3. The visualization will dim/hide matching folders

### Import/Export

**Import JSON:**
- Click **Import JSON**
- Select a previously exported JSON file
- Perfect for analyzing scans from remote machines

**Export Data:**
- Click **Export** to save current view as JSON
- Useful for sharing analysis or documentation

### Running in ConnectWise Automate Backstage

The application works perfectly in backstage mode (SYSTEM context):

1. Upload `DiskSpaceAnalyzer.exe` to the remote machine
2. Launch via backstage or as a script
3. The GUI will display on the remote desktop

Alternatively, you can:
1. Scan the drive using PowerShell (generate JSON)
2. Download the JSON to your local machine
3. Open it in the visualizer locally

## PowerShell Scanner (Optional)

If you prefer to scan remotely and analyze locally, use this PowerShell script:

```powershell
# Save as: Get-DiskSpace.ps1
$path = "C:\"
$maxDepth = 3

function Get-FolderSize {
    param($Path, $CurrentDepth = 0, $MaxDepth = 3)
    
    if ($CurrentDepth -gt $MaxDepth) { return $null }
    
    try {
        $items = Get-ChildItem -Path $Path -Force -ErrorAction SilentlyContinue
        $size = 0
        $children = @()
        
        foreach ($item in $items) {
            if ($item.PSIsContainer) {
                $childData = Get-FolderSize -Path $item.FullName -CurrentDepth ($CurrentDepth + 1) -MaxDepth $MaxDepth
                if ($childData) {
                    $children += $childData
                    $size += $childData.size
                }
            } else {
                $size += $item.Length
            }
        }
        
        return @{
            name = $Path | Split-Path -Leaf
            path = $Path
            size = $size
            type = "folder"
            children = $children
        }
    } catch {
        return $null
    }
}

$result = Get-FolderSize -Path $path -MaxDepth $maxDepth
$result | ConvertTo-Json -Depth 10 | Out-File "disk-data.json"
```

Run this on the target machine, then import the JSON into the visualizer.

## Color Legend

- **Red (>50%)** - Largest folders
- **Orange (30-50%)** - Large folders
- **Amber (15-30%)** - Medium folders
- **Yellow (5-15%)** - Smaller folders
- **Lime (2-5%)** - Small folders
- **Green (<2%)** - Smallest folders

## Tips for MSP Use

1. **Scan Depth:** The default scan depth is 3 levels. Increase for more detail (slower) or decrease for faster scans.
2. **System Volume Information:** The scanner includes this folder by default (including snapshots).
3. **Filtering:** When Users folder dominates the view, filter it to see system folders.
4. **Remote Analysis:** For slow/problematic scans, use PowerShell to generate JSON remotely, then analyze locally.
5. **Documentation:** Export JSON files with timestamps for client documentation.

## Troubleshooting

**"Access Denied" errors during scan:**
- Run as Administrator
- Some system folders require elevated privileges
- In backstage/SYSTEM mode, you should have full access

**Application won't start:**
- Ensure .NET Framework 4.8 is installed
- Check Windows Event Viewer for error details

**Scan takes too long:**
- Reduce scan depth (modify `FileSystemScanner` constructor)
- Use PowerShell script for initial scan instead

## Architecture

The application consists of:
- **MainForm.cs** - Main UI and application logic
- **TreeMapPanel.cs** - Custom control for treemap rendering
- **FileSystemScanner.cs** - Filesystem scanning and JSON serialization
- **FileSystemNode.cs** - Data model for filesystem hierarchy

## License

Free to use for MSP work. Feel free to modify and distribute.

## Version

Version 1.0 - Initial Release
