using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DiskSpaceAnalyzer
{
    public class TreeMapPanel : Panel
    {
        private FileSystemNode currentNode;
        private string filterText = "";
        private FileSystemNode hoveredNode = null;
        private FileSystemNode rightClickedNode = null;
        private Dictionary<Rectangle, FileSystemNode> nodeRectangles = new Dictionary<Rectangle, FileSystemNode>();
        private int zoomLevel = 4;
        private ContextMenuStrip contextMenu;

        public event EventHandler<FileSystemNode> NodeClicked;
        public event EventHandler<FileSystemNode> FolderDeleted;

        public int ZoomLevel
        {
            get => zoomLevel;
            set
            {
                zoomLevel = Math.Max(1, Math.Min(5, value));
                this.Invalidate();
            }
        }

        public TreeMapPanel()
        {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(15, 23, 42);
            this.MouseMove += TreeMapPanel_MouseMove;
            this.MouseClick += TreeMapPanel_MouseClick;
            this.MouseLeave += TreeMapPanel_MouseLeave;

            // Setup context menu
            SetupContextMenu();
        }

        private void SetupContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            var openPathItem = new ToolStripMenuItem("Open Folder in Explorer");
            openPathItem.Click += (s, e) => OpenPath();
            contextMenu.Items.Add(openPathItem);

            var openFileItem = new ToolStripMenuItem("Open File");
            openFileItem.Click += (s, e) => OpenFile();
            contextMenu.Items.Add(openFileItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var deleteItem = new ToolStripMenuItem("Delete");
            deleteItem.Click += (s, e) => DeleteItem();
            contextMenu.Items.Add(deleteItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var propertiesItem = new ToolStripMenuItem("Properties");
            propertiesItem.Click += (s, e) => ShowProperties();
            contextMenu.Items.Add(propertiesItem);

            // Update menu items based on what was right-clicked
            contextMenu.Opening += (s, e) =>
            {
                if (rightClickedNode == null)
                {
                    e.Cancel = true;
                    return;
                }

                bool isFile = rightClickedNode.Type == "file";
                openFileItem.Visible = isFile;
                openPathItem.Text = isFile ? "Open Containing Folder" : "Open Folder in Explorer";
            };

            this.ContextMenuStrip = contextMenu;
        }

        private void TreeMapPanel_MouseLeave(object sender, EventArgs e)
        {
            if (hoveredNode != null)
            {
                hoveredNode = null;
                this.Invalidate();
            }
        }

        private void TreeMapPanel_MouseMove(object sender, MouseEventArgs e)
        {
            FileSystemNode newHover = null;
            foreach (var kvp in nodeRectangles)
            {
                if (kvp.Key.Contains(e.Location))
                {
                    newHover = kvp.Value;
                    break;
                }
            }

            if (newHover != hoveredNode)
            {
                hoveredNode = newHover;
                this.Cursor = (hoveredNode != null && hoveredNode.Children.Count > 0) ? Cursors.Hand : Cursors.Default;
                this.Invalidate();
            }
        }

        private void TreeMapPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Find which node was clicked at this exact position
                rightClickedNode = FindNodeAtPoint(e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                // Left-click - navigate if it's a folder
                if (hoveredNode != null && hoveredNode.Children.Count > 0)
                {
                    NodeClicked?.Invoke(this, hoveredNode);
                }
            }
        }

        private FileSystemNode FindNodeAtPoint(Point location)
        {
            // Find ALL rectangles that contain this point
            var matchingNodes = nodeRectangles
                .Where(kvp => kvp.Key.Contains(location))
                .ToList();

            if (matchingNodes.Count == 0)
                return null;

            // Return the SMALLEST rectangle (most nested/specific)
            // When boxes are nested, we want the innermost one
            return matchingNodes
                .OrderBy(kvp => kvp.Key.Width * kvp.Key.Height)
                .First()
                .Value;
        }

        public void SetData(FileSystemNode node, string filter = "")
        {
            currentNode = node;
            filterText = filter;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (currentNode == null || currentNode.Children.Count == 0)
            {
                using (var font = new Font("Consolas", 12, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    string message = currentNode == null ? "No data loaded" : "No items to display";
                    var size = e.Graphics.MeasureString(message, font);
                    e.Graphics.DrawString(message, font, brush,
                        (this.Width - size.Width) / 2, (this.Height - size.Height) / 2);
                }
                return;
            }

            nodeRectangles.Clear();
            var visibleChildren = currentNode.GetVisibleChildren(filterText);

            if (visibleChildren.Count == 0)
            {
                using (var font = new Font("Consolas", 12, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                {
                    string message = "All items filtered";
                    var size = e.Graphics.MeasureString(message, font);
                    e.Graphics.DrawString(message, font, brush,
                        (this.Width - size.Width) / 2, (this.Height - size.Height) / 2);
                }
                return;
            }

            // Show ONLY the direct children - let the nested drawing handle depth
            long totalSize = visibleChildren.Sum(c => c.Size);
            var sortedChildren = visibleChildren.OrderByDescending(c => c.Size).ToList();

            DrawTreeMap(e.Graphics, sortedChildren, new Rectangle(5, 5, this.Width - 10, this.Height - 10), totalSize);
        }

        private void DrawTreeMap(Graphics g, List<FileSystemNode> nodes, Rectangle bounds, long totalSize)
        {
            if (nodes.Count == 0 || bounds.Width < 10 || bounds.Height < 10 || totalSize == 0)
                return;

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Use binary split with nesting support
            BinarySplit(g, nodes, bounds, totalSize, 0);
        }

        private void BinarySplit(Graphics g, List<FileSystemNode> nodes, Rectangle bounds, long totalSize, int depth)
        {
            if (nodes.Count == 0 || totalSize == 0)
                return;

            // Minimum box sizes - reduced to show tiny files like SpaceSniffer
            int minWidth = 8;   // Was 20
            int minHeight = 8;  // Was 15

            if (bounds.Width < minWidth || bounds.Height < minHeight)
                return;

            // Single item - draw it and maybe its children
            if (nodes.Count == 1)
            {
                DrawNodeWithChildren(g, nodes[0], bounds, totalSize, depth);
                return;
            }

            // Split items into two lists
            var list1 = new List<FileSystemNode>();
            var list2 = new List<FileSystemNode>();
            long list1Sum = 0;
            long list2Sum = 0;

            foreach (var node in nodes)
            {
                if (list1Sum <= list2Sum)
                {
                    list1.Add(node);
                    list1Sum += node.Size;
                }
                else
                {
                    list2.Add(node);
                    list2Sum += node.Size;
                }
            }

            if (list1.Count == 0 || list2.Count == 0)
            {
                DrawNodeWithChildren(g, nodes[0], bounds, totalSize, depth);
                return;
            }

            bool splitHorizontally = bounds.Width > bounds.Height;
            Rectangle rect1, rect2;
            long combinedSize = list1Sum + list2Sum;

            if (splitHorizontally)
            {
                int split = combinedSize > 0 ? (int)((double)bounds.Width * list1Sum / combinedSize) : bounds.Width / 2;
                split = Math.Max(minWidth, Math.Min(split, bounds.Width - minWidth));

                rect1 = new Rectangle(bounds.X, bounds.Y, split, bounds.Height);
                rect2 = new Rectangle(bounds.X + split, bounds.Y, bounds.Width - split, bounds.Height);
            }
            else
            {
                int split = combinedSize > 0 ? (int)((double)bounds.Height * list1Sum / combinedSize) : bounds.Height / 2;
                split = Math.Max(minHeight, Math.Min(split, bounds.Height - minHeight));

                rect1 = new Rectangle(bounds.X, bounds.Y, bounds.Width, split);
                rect2 = new Rectangle(bounds.X, bounds.Y + split, bounds.Width, bounds.Height - split);
            }

            // Recursively split
            if (list1.Count > 1 && rect1.Width >= minWidth && rect1.Height >= minHeight)
                BinarySplit(g, list1, rect1, totalSize, depth);
            else if (list1.Count > 0 && rect1.Width >= minWidth && rect1.Height >= minHeight)
                DrawNodeWithChildren(g, list1[0], rect1, totalSize, depth);

            if (list2.Count > 1 && rect2.Width >= minWidth && rect2.Height >= minHeight)
                BinarySplit(g, list2, rect2, totalSize, depth);
            else if (list2.Count > 0 && rect2.Width >= minWidth && rect2.Height >= minHeight)
                DrawNodeWithChildren(g, list2[0], rect2, totalSize, depth);
        }

        private void DrawNodeWithChildren(Graphics g, FileSystemNode node, Rectangle bounds, long totalSize, int depth)
        {
            // Draw the parent node background and border first
            bool isHovered = node == hoveredNode;
            Color bgColor = GetColorForSize(node.Size, totalSize);

            // Shrink rect for padding
            Rectangle rect = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);

            // Draw background
            using (var brush = new SolidBrush(bgColor))
            {
                g.FillRectangle(brush, rect);
            }

            // Draw subtle inner shadow for depth
            using (var shadowPen = new Pen(Color.FromArgb(20, 0, 0, 0)))
            {
                g.DrawLine(shadowPen, rect.X, rect.Y, rect.Right, rect.Y);
                g.DrawLine(shadowPen, rect.X, rect.Y, rect.X, rect.Bottom);
            }

            // Draw clean border - thicker for hover
            Color borderColor = isHovered ? Color.FromArgb(255, 255, 255) : Color.FromArgb(40, 0, 0, 0);
            int borderWidth = isHovered ? 2 : 1;

            using (var pen = new Pen(borderColor, borderWidth))
            {
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            // Determine if we should show children
            bool hasFilter = !string.IsNullOrEmpty(filterText);
            bool shouldDrillDown = (node.Type == "folder" && node.Children.Count > 0);

            // If it's a folder with children, draw them inside
            if (shouldDrillDown && bounds.Width > 25 && bounds.Height > 20)
            {
                // Calculate space for children - keep it tight like SpaceSniffer
                int headerHeight = Math.Max(16, Math.Min(22, bounds.Height / 8));  // Back to tight spacing
                int padding = 2;

                Rectangle childBounds = new Rectangle(
                    bounds.X + padding + 1,
                    bounds.Y + headerHeight + 1,
                    bounds.Width - (padding * 2) - 2,
                    bounds.Height - headerHeight - padding - 2
                );

                if (childBounds.Width > 25 && childBounds.Height > 20)
                {
                    var visibleChildren = node.GetVisibleChildren(filterText)
                        .OrderByDescending(c => c.Size)
                        .ToList();

                    if (visibleChildren.Count > 0)
                    {
                        long childTotal = visibleChildren.Sum(c => c.Size);
                        BinarySplit(g, visibleChildren, childBounds, totalSize, depth + 1);
                    }
                }
            }

            // Draw the parent label LAST (on top of children)
            DrawNodeTextOnly(g, node, rect, totalSize);

            // Store rectangle for hit testing
            nodeRectangles[rect] = node;
        }

        private void DrawNodeTextOnly(Graphics g, FileSystemNode node, Rectangle rect, long totalSize)
        {
            // Draw text even on small boxes - but not TOO small
            if (rect.Width < 20 || rect.Height < 12)
                return;

            using (var textBrush = new SolidBrush(Color.Black))
            {
                float fontSize = 7f;
                int padding = 2;  // Back to 2 for tight spacing

                using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                {
                    if (rect.Width <= padding * 2 || rect.Height <= padding * 2)
                        return;

                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
                    };

                    bool showSize = rect.Width > 180;
                    string text = showSize ?
                        $"{node.Name} ({FileSystemNode.FormatBytes(node.Size)})" :
                        node.Name;

                    RectangleF textRect = new RectangleF(
                        rect.X + padding,
                        rect.Y + padding,
                        rect.Width - (padding * 2),
                        rect.Height - (padding * 2)
                    );

                    // Draw subtle drop shadow
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    {
                        RectangleF shadowRect = new RectangleF(
                            textRect.X + 1,
                            textRect.Y + 1,
                            textRect.Width,
                            textRect.Height
                        );
                        g.DrawString(text, font, shadowBrush, shadowRect, format);
                    }

                    g.DrawString(text, font, textBrush, textRect, format);
                }
            }
        }

        private bool ShouldFilterOut(string name)
        {
            if (string.IsNullOrWhiteSpace(filterText))
                return false;

            // Split by comma to support multiple filters
            var filters = filterText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(f => f.Trim())
                                     .Where(f => !string.IsNullOrEmpty(f))
                                     .ToList();

            if (filters.Count == 0)
                return false;

            // Separate include and exclude filters
            var includeFilters = filters.Where(f => !f.StartsWith("!")).ToList();
            var excludeFilters = filters.Where(f => f.StartsWith("!")).Select(f => f.Substring(1).Trim()).ToList();

            // Check exclude filters first
            foreach (var exclude in excludeFilters)
            {
                if (!string.IsNullOrEmpty(exclude) &&
                    name.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true; // Filter out
                }
            }

            // If there are include filters, name must match at least one
            if (includeFilters.Count > 0)
            {
                bool matchesAny = false;
                foreach (var include in includeFilters)
                {
                    if (!string.IsNullOrEmpty(include) &&
                        name.IndexOf(include, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchesAny = true;
                        break;
                    }
                }
                return !matchesAny; // Filter out if doesn't match any include
            }

            return false; // Don't filter out
        }

        private double GetShortestSide(Rectangle rect)
        {
            return Math.Min(rect.Width, rect.Height);
        }

        private void DrawSingleNode(Graphics g, FileSystemNode node, Rectangle rect, long totalSize)
        {
            if (rect.Width <= 5 || rect.Height <= 5) return;

            DrawNode(g, node, rect, totalSize);
            nodeRectangles[rect] = node;
        }

        private void DrawNode(Graphics g, FileSystemNode node, Rectangle rect, long totalSize)
        {
            bool isHovered = node == hoveredNode;
            Color bgColor = GetColorForSize(node.Size, totalSize);

            // Shrink rect for padding
            rect = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);

            // Draw background
            using (var brush = new SolidBrush(bgColor))
            {
                g.FillRectangle(brush, rect);
            }

            // Draw subtle inner shadow for depth
            using (var shadowPen = new Pen(Color.FromArgb(20, 0, 0, 0)))
            {
                g.DrawLine(shadowPen, rect.X, rect.Y, rect.Right, rect.Y);
                g.DrawLine(shadowPen, rect.X, rect.Y, rect.X, rect.Bottom);
            }

            // Draw clean border - thicker for hover
            Color borderColor = isHovered ? Color.FromArgb(255, 255, 255) : Color.FromArgb(40, 0, 0, 0);
            int borderWidth = isHovered ? 2 : 1;

            using (var pen = new Pen(borderColor, borderWidth))
            {
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            }

            // Draw text even on small boxes - but not TOO small
            if (rect.Width < 20 || rect.Height < 12)  // Only skip VERY tiny boxes
                return;

            // Draw text with FIXED font size like SpaceSniffer
            using (var textBrush = new SolidBrush(Color.Black))
            {
                // FIXED 7pt font - compact like SpaceSniffer
                float fontSize = 7f;
                int padding = 3;  // Increased from 2 to 3 for better edge spacing

                using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold))
                {
                    // Ensure we have enough space for text
                    if (rect.Width <= padding * 2 || rect.Height <= padding * 2)
                        return;

                    // Don't clip - let text overflow naturally
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
                    };

                    // Show size only on boxes wider than 180px
                    bool showSize = rect.Width > 180;
                    string text = showSize ?
                        $"{node.Name} ({FileSystemNode.FormatBytes(node.Size)})" :
                        node.Name;

                    // Text rectangle with proper padding
                    RectangleF textRect = new RectangleF(
                        rect.X + padding,
                        rect.Y + padding,
                        rect.Width - (padding * 2),
                        rect.Height - (padding * 2)
                    );

                    // Draw subtle drop shadow (1px offset, semi-transparent white)
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                    {
                        RectangleF shadowRect = new RectangleF(
                            textRect.X + 1,
                            textRect.Y + 1,
                            textRect.Width,
                            textRect.Height
                        );
                        g.DrawString(text, font, shadowBrush, shadowRect, format);
                    }

                    // Draw BLACK text on top (high contrast!)
                    g.DrawString(text, font, textBrush, textRect, format);
                }
            }
        }

        private Color GetColorForSize(long size, long maxSize)
        {
            double ratio = (double)size / maxSize;

            if (ratio > 0.5) return Color.FromArgb(220, 38, 38);   // red-600
            if (ratio > 0.3) return Color.FromArgb(234, 88, 12);   // orange-600
            if (ratio > 0.15) return Color.FromArgb(245, 158, 11); // amber-500
            if (ratio > 0.05) return Color.FromArgb(234, 179, 8);  // yellow-500
            if (ratio > 0.02) return Color.FromArgb(132, 204, 22); // lime-500
            return Color.FromArgb(34, 197, 94);                     // green-500
        }

        private void OpenPath()
        {
            if (rightClickedNode == null) return;

            try
            {
                string path = rightClickedNode.Path;
                if (rightClickedNode.Type == "file")
                {
                    // Open containing folder and select the file
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    // Open the folder
                    System.Diagnostics.Process.Start("explorer.exe", path);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open path: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenFile()
        {
            if (rightClickedNode == null || rightClickedNode.Type != "file") return;

            try
            {
                System.Diagnostics.Process.Start(rightClickedNode.Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteItem()
        {
            if (rightClickedNode == null) return;

            string itemType = rightClickedNode.Type == "file" ? "file" : "folder";
            string itemName = rightClickedNode.Name;
            long itemSize = rightClickedNode.Size;

            var result = MessageBox.Show(
                $"âš ï¸ DELETE CONFIRMATION âš ï¸\n\n" +
                $"Type: {itemType.ToUpper()}\n" +
                $"Name: {itemName}\n" +
                $"Size: {FileSystemNode.FormatBytes(itemSize)}\n" +
                $"Path: {rightClickedNode.Path}\n\n" +
                $"This will move the {itemType} to the Recycle Bin.\n\n" +
                $"Are you SURE you want to delete this {itemType}?",
                "âš ï¸ Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2  // Default to "No"
            );

            if (result == DialogResult.Yes)
            {
                try
                {
                    if (rightClickedNode.Type == "file")
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            rightClickedNode.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin
                        );
                    }
                    else
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                            rightClickedNode.Path,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin
                        );
                    }

                    MessageBox.Show($"{itemType.ToUpper()} deleted successfully!\n\n{itemName}\n\nYou can restore it from the Recycle Bin if needed.",
                        "âœ“ Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    FolderDeleted?.Invoke(this, rightClickedNode);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"âŒ Failed to delete {itemType}:\n\n{ex.Message}\n\nPath: {rightClickedNode.Path}",
                        "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // P/Invoke for showing properties dialog
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpVerb;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpFile;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpParameters;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 12;

        private void ShowProperties()
        {
            if (rightClickedNode == null) return;

            try
            {
                SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(info);
                info.lpVerb = "properties";
                info.lpFile = rightClickedNode.Path;
                info.nShow = 5; // SW_SHOW
                info.fMask = SEE_MASK_INVOKEIDLIST;

                ShellExecuteEx(ref info);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show properties: {ex.Message}\n\nPath: {rightClickedNode.Path}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}