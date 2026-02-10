# Version 2 - Bug Fixes

## Issues Fixed:

### 1. Text Visibility Issues
**Problem:** Text was not visible or too small to read
**Fix:** 
- Changed to white text with black shadow on all boxes for maximum visibility
- Increased minimum font sizes (8-10pt based on box size)
- Improved text contrast against all background colors
- Better padding around text

### 2. Added Zoom/Detail Control
**Problem:** No way to control how much detail is shown
**Fix:**
- Added **Detail +/-** buttons next to the toolbar
- **Detail levels 1-5:**
  - **Level 1** (minimal): Shows only immediate children (like SpaceSniffer minimal)
  - **Level 2** (default): Shows 2 levels deep (good balance)
  - **Level 3**: Shows 3 levels deep
  - **Level 4**: Shows 4 levels deep  
  - **Level 5** (max): Shows 5 levels deep (most detail, smallest boxes)
- Click **+** to see more detail (smaller boxes, more items)
- Click **-** to see less detail (larger boxes, fewer items)

### 3. Filter Not Working
**Problem:** Filter wasn't hiding items
**Fix:**
- Fixed the filter logic to properly exclude items whose path contains the filter text
- Added trim() to handle extra spaces
- Filter now immediately updates the visualization

### 4. Import Showing Blank Screen
**Problem:** Imported JSON wasn't displaying
**Fix:**
- Added proper view refresh after import
- Enabled zoom buttons after import
- Now shows the visualization immediately after loading

### 5. Better Layout Algorithm
**Problem:** Boxes were too large and text was crushed
**Fix:**
- Switched to simpler proportional layout algorithm
- Better space distribution
- Minimum box sizes to ensure text is readable
- Smoother transitions between detail levels

## Usage Tips:

1. **Start with Detail Level 2** (default) for best overview
2. **Increase detail** (+) when you want to see more subfolder breakdown
3. **Decrease detail** (-) when boxes get too small to read
4. **Use Filter** to hide large folders (like "Users") to see the rest
5. **Click boxes** to drill into folders for detailed view

## Known Behavior:

- Scan speed is fast because it only goes 3 levels deep by default
- This is intentional for MSP quick diagnostics
- For deeper analysis, you can drill down by clicking folders
- The detail level affects visualization only, not the scan depth
