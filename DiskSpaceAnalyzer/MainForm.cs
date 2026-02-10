using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace DiskSpaceAnalyzer
{
    public partial class MainForm : Form
    {
        private FileSystemNode rootNode;
        private FileSystemNode currentNode;
        private Stack<FileSystemNode> navigationHistory;
        private TreeMapPanel treeMapPanel;
        private TextBox filterTextBox;
        private Label statusLabel;
        private Label breadcrumbLabel;
        private Button scanButton;
        private Button importButton;
        private Button exportButton;
        private Button homeButton;
        private Button backButton;
        private Button filterButton;
        private Panel filterPanel;
        //        private Panel legendPanel;

        public MainForm()
        {
            InitializeComponent();
            navigationHistory = new Stack<FileSystemNode>();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Disk Space Analyzer - MSP Tool";
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(800, 600);
            this.BackColor = Color.FromArgb(15, 23, 42); // slate-900
            this.StartPosition = FormStartPosition.CenterScreen;

            // Top toolbar panel - SINGLE LINE
            var toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,  // Single line height
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(10, 5, 10, 5)
            };

            // Buttons panel - left side, single row
            var buttonsPanel = new Panel
            {
                Location = new Point(10, 5),
                Size = new Size(700, 35),  // Fixed width for buttons
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            int buttonX = 0;

            // Scan button
            scanButton = CreateButton("Scan Drive", buttonX);
            scanButton.Click += ScanButton_Click;
            buttonsPanel.Controls.Add(scanButton);
            buttonX += 120;

            // Import button
            importButton = CreateButton("Import JSON", buttonX);
            importButton.Click += ImportButton_Click;
            buttonsPanel.Controls.Add(importButton);
            buttonX += 120;

            // Export button with dropdown menu
            exportButton = CreateButton("Export", buttonX);
            exportButton.Enabled = false;

            // Create context menu for export options
            var exportMenu = new ContextMenuStrip();
            exportMenu.BackColor = Color.FromArgb(30, 41, 59);
            exportMenu.ForeColor = Color.White;
            exportMenu.Font = new Font("Segoe UI", 9);

            var exportJsonItem = new ToolStripMenuItem("Export to JSON");
            exportJsonItem.Click += ExportButton_Click;
            exportMenu.Items.Add(exportJsonItem);

            var exportPngItem = new ToolStripMenuItem("Export to PNG (Image)");
            exportPngItem.Click += ExportToPng_Click;
            exportMenu.Items.Add(exportPngItem);

            var exportExcelItem = new ToolStripMenuItem("Export to CSV (Excel-Compatible)");
            exportExcelItem.Click += ExportToExcel_Click;
            exportMenu.Items.Add(exportExcelItem);

            var exportHtmlItem = new ToolStripMenuItem("Export to HTML (Interactive Report)");
            exportHtmlItem.Click += ExportToHtml_Click;
            exportMenu.Items.Add(exportHtmlItem);

            exportButton.Click += (s, e) =>
            {
                exportMenu.Show(exportButton, new Point(0, exportButton.Height));
            };

            buttonsPanel.Controls.Add(exportButton);
            buttonX += 120;

            // Home button
            homeButton = CreateButton("Home", buttonX);
            homeButton.Click += HomeButton_Click;
            homeButton.Enabled = false;
            buttonsPanel.Controls.Add(homeButton);
            buttonX += 120;

            // Back button
            backButton = CreateButton("Back", buttonX);
            backButton.Click += BackButton_Click;
            backButton.Enabled = false;
            buttonsPanel.Controls.Add(backButton);
            buttonX += 120;

            // Filter button
            filterButton = CreateButton("Filter", buttonX);
            filterButton.Click += FilterButton_Click;
            filterButton.Enabled = false;
            buttonsPanel.Controls.Add(filterButton);
            buttonX += 120;

            // Zoom label
            var zoomLabel = new Label
            {
                Text = "Depth:",
                Location = new Point(buttonX + 10, 8),
                AutoSize = true,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            buttonsPanel.Controls.Add(zoomLabel);
            buttonX += 55;

            // Declare zoom buttons first (before using them in event handlers)
            Button zoomOutButton = null;
            Button zoomInButton = null;

            // Zoom out button
            zoomOutButton = new Button
            {
                Text = "-",
                Location = new Point(buttonX, 0),
                Size = new Size(35, 35),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            zoomOutButton.FlatAppearance.BorderSize = 0;
            zoomOutButton.Click += (s, e) =>
            {
                treeMapPanel.ZoomLevel--;
                UpdateZoomButtons(zoomOutButton, zoomInButton);
            };
            buttonsPanel.Controls.Add(zoomOutButton);
            buttonX += 40;

            // Zoom in button
            zoomInButton = new Button
            {
                Text = "+",
                Location = new Point(buttonX, 0),
                Size = new Size(35, 35),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            zoomInButton.FlatAppearance.BorderSize = 0;
            zoomInButton.Click += (s, e) =>
            {
                treeMapPanel.ZoomLevel++;
                UpdateZoomButtons(zoomOutButton, zoomInButton);
            };
            buttonsPanel.Controls.Add(zoomInButton);

            // Store references
            this.Tag = new { ZoomOut = zoomOutButton, ZoomIn = zoomInButton };

            toolbarPanel.Controls.Add(buttonsPanel);


            // Compact legend on right side - SAME LINE as buttons
            int legendX = 720;
            AddCompactLegendItem(toolbarPanel, Color.FromArgb(220, 38, 38), ">50%", legendX, 10);
            legendX += 70;
            AddCompactLegendItem(toolbarPanel, Color.FromArgb(234, 88, 12), "30-50%", legendX, 10);
            legendX += 75;
            AddCompactLegendItem(toolbarPanel, Color.FromArgb(245, 158, 11), "15-30%", legendX, 10);
            legendX += 75;
            AddCompactLegendItem(toolbarPanel, Color.FromArgb(234, 179, 8), "5-15%", legendX, 10);
            legendX += 65;
            AddCompactLegendItem(toolbarPanel, Color.FromArgb(132, 204, 22), "2-5%", legendX, 10);
            legendX += 55;
            AddCompactLegendItem(toolbarPanel, Color.FromArgb(34, 197, 94), "<2%", legendX, 10);

            // Filter panel (initially hidden)
            filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(10),
                Visible = false
            };

            var filterLabel = new Label
            {
                Text = "Filter:",
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(10, 15),
                AutoSize = true
            };
            filterPanel.Controls.Add(filterLabel);

            filterTextBox = new TextBox
            {
                Location = new Point(60, 12),
                Size = new Size(100, 25),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            filterTextBox.TextChanged += FilterTextBox_TextChanged;
            filterPanel.Controls.Add(filterTextBox);

            // Breadcrumb/Status bar - thin bar below toolbar (SHARED - shows breadcrumb OR status)
            var breadcrumbPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 25,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(10, 0, 10, 0)
            };

            // Combined breadcrumb + status label (will show either breadcrumb or scan progress)
            breadcrumbLabel = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8f),
                Text = "No data loaded",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
            breadcrumbPanel.Controls.Add(breadcrumbLabel);

            // Store reference to breadcrumb label as statusLabel too (they're the same now)
            statusLabel = breadcrumbLabel;


            // SMART status label - ON THE RIGHT SIDE OF BREADCRUMB BAR
            var smartStatusLabel = new Label
            {
                Name = "smartStatusLabel",
                Dock = DockStyle.Right,
                Width = 200,
                ForeColor = Color.FromArgb(34, 197, 94),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Text = "",
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0),
                AutoSize = false
            };
            breadcrumbPanel.Controls.Add(smartStatusLabel);

            // Help button - anchored to right
            var helpButton = new Button
            {
                Text = "?",
                Location = new Point(10, 10), // Temporary, will be updated
                Size = new Size(30, 30),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.FromArgb(148, 163, 184),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            helpButton.FlatAppearance.BorderSize = 1;
            helpButton.FlatAppearance.BorderColor = Color.FromArgb(71, 85, 105);
            helpButton.Click += (s, e) =>
            {
                MessageBox.Show(
                    "FILTER USAGE:\n\n" +
                    "âœ… NEW: Include filters now search RECURSIVELY!\n" +
                    "   Typing \"takeout\" shows folders containing takeout files\n\n" +
                    "INCLUDE (show only matching items):\n" +
                    "   Type: takeout\n" +
                    "   Shows: Items with \"takeout\" OR folders containing them\n\n" +
                    "EXCLUDE (hide matching items):\n" +
                    "   Type: !Users\n" +
                    "   Shows: Everything EXCEPT items with \"Users\"\n\n" +
                    "MULTIPLE FILTERS (comma separated):\n" +
                    "   Type: Program, !Data\n" +
                    "   Shows: Items with \"Program\" but not \"Data\"\n\n" +
                    "   Type: !temp, !cache\n" +
                    "   Shows: Everything except temp or cache\n\n" +
                    "EXAMPLES:\n" +
                    "   adobe          â†’ Show Adobe items anywhere\n" +
                    "   !windows       â†’ Hide all Windows folders\n" +
                    "   .zip           â†’ Show .zip files anywhere\n" +
                    "   2024, !backup  â†’ Show 2024 items except backups",
                    "Filter Help",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };
            filterPanel.Controls.Add(helpButton);

            var clearFilterButton = new Button
            {
                Text = "Clear",
                Location = new Point(10, 10), // Temporary, will be updated
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(51, 65, 85),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            clearFilterButton.Click += (s, e) => filterTextBox.Text = "";
            filterPanel.Controls.Add(clearFilterButton);

            // Layout handler to position buttons correctly
            filterPanel.Resize += (s, e) =>
            {
                int panelWidth = filterPanel.ClientSize.Width;
                clearFilterButton.Left = panelWidth - clearFilterButton.Width - 10;
                helpButton.Left = clearFilterButton.Left - helpButton.Width - 10;
                filterTextBox.Width = helpButton.Left - filterTextBox.Left - 10;
            };

            // TreeMap panel - fills entire space
            treeMapPanel = new TreeMapPanel
            {
                Dock = DockStyle.Fill
            };
            treeMapPanel.NodeClicked += TreeMapPanel_NodeClicked;
            this.Controls.Add(treeMapPanel);

            // Add top panels LAST so they dock on top (correct order: breadcrumb, filter, toolbar)
            this.Controls.Add(breadcrumbPanel);
            this.Controls.Add(filterPanel);
            this.Controls.Add(toolbarPanel);

            // Hook up Form Load event to check SMART on startup
            this.Load += MainForm_Load;

            this.ResumeLayout(false);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Check SMART status for C: drive on startup
            UpdateSmartStatus(@"C:\");
        }

        private void CountItems(FileSystemNode node, ref int folders, ref int files)
        {
            if (node == null) return;

            if (node.Type == "folder")
            {
                folders++;
                foreach (var child in node.Children)
                {
                    CountItems(child, ref folders, ref files);
                }
            }
            else
            {
                files++;
            }
        }

        private Button CreateButton(string text, int x)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, 0),
                Size = new Size(110, 35),
                BackColor = Color.FromArgb(8, 145, 178), // cyan-600
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void AddLegendItem(Panel panel, Color color, string text, int x)
        {
            var colorBox = new Panel
            {
                Location = new Point(x, 15),
                Size = new Size(20, 20),
                BackColor = color
            };
            panel.Controls.Add(colorBox);

            var label = new Label
            {
                Text = text,
                Location = new Point(x + 25, 15),
                AutoSize = true,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8)
            };
            panel.Controls.Add(label);
        }

        private void AddCompactLegendItem(Panel panel, Color color, string text, int x, int y)
        {
            var colorBox = new Panel
            {
                Location = new Point(x, y + 2),
                Size = new Size(12, 12),  // Smaller box
                BackColor = color
            };
            panel.Controls.Add(colorBox);

            var label = new Label
            {
                Text = text,
                Location = new Point(x + 16, y),
                AutoSize = true,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7f)  // Smaller font
            };
            panel.Controls.Add(label);
        }

        private async void ScanButton_Click(object sender, EventArgs e)
        {
            // Get list of available drives
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            if (drives.Count == 0)
            {
                MessageBox.Show("No fixed drives found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // If only one drive, use it automatically
            string selectedPath;
            if (drives.Count == 1)
            {
                selectedPath = drives[0].RootDirectory.FullName;
            }
            else
            {
                // Show simple drive selection dialog
                using (var driveForm = new Form())
                {
                    driveForm.Text = "Select Drive to Scan";
                    driveForm.Size = new Size(400, 300);
                    driveForm.StartPosition = FormStartPosition.CenterParent;
                    driveForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    driveForm.MaximizeBox = false;
                    driveForm.MinimizeBox = false;

                    var listBox = new ListBox
                    {
                        Dock = DockStyle.Fill,
                        Font = new Font("Segoe UI", 10)
                    };

                    foreach (var drive in drives)
                    {
                        string label = $"{drive.Name} - {drive.VolumeLabel} ({FileSystemNode.FormatBytes(drive.TotalSize)})";
                        listBox.Items.Add(new { Drive = drive, Label = label });
                    }
                    listBox.DisplayMember = "Label";
                    listBox.SelectedIndex = 0;

                    var okButton = new Button
                    {
                        Text = "Scan",
                        DialogResult = DialogResult.OK,
                        Dock = DockStyle.Bottom,
                        Height = 40
                    };

                    driveForm.Controls.Add(listBox);
                    driveForm.Controls.Add(okButton);
                    driveForm.AcceptButton = okButton;

                    if (driveForm.ShowDialog() != DialogResult.OK || listBox.SelectedItem == null)
                        return;

                    var selected = (dynamic)listBox.SelectedItem;
                    selectedPath = selected.Drive.RootDirectory.FullName;
                }
            }

            // Proceed with scan
            scanButton.Enabled = false;
            importButton.Enabled = false;
            statusLabel.Text = "Scanning...";

            try
            {
                var scanner = new FileSystemScanner();  // No depth limit - scan everything!
                scanner.StatusChanged += (s, status) =>
                {
                    this.Invoke((Action)(() => statusLabel.Text = status));
                };

                rootNode = await scanner.ScanAsync(selectedPath);

                if (rootNode != null)
                {
                    // Count total items for debugging
                    int totalFolders = 0;
                    int totalFiles = 0;
                    CountItems(rootNode, ref totalFolders, ref totalFiles);

                    System.Diagnostics.Debug.WriteLine($"========================================");
                    System.Diagnostics.Debug.WriteLine($"SCAN COMPLETE: {selectedPath}");
                    System.Diagnostics.Debug.WriteLine($"Total Size: {FileSystemNode.FormatBytes(rootNode.Size)}");
                    System.Diagnostics.Debug.WriteLine($"Total Folders: {totalFolders:N0}");
                    System.Diagnostics.Debug.WriteLine($"Total Files: {totalFiles:N0}");
                    System.Diagnostics.Debug.WriteLine($"Total Items: {(totalFolders + totalFiles):N0}");
                    System.Diagnostics.Debug.WriteLine($"========================================");

                    LoadData(rootNode);
                    statusLabel.Text = $"Scan complete: {FileSystemNode.FormatBytes(rootNode.Size)} ({totalFolders + totalFiles:N0} items)";

                    // Check SMART status for the drive
                    UpdateSmartStatus(selectedPath);
                }
                else
                {
                    MessageBox.Show("Failed to scan directory. Access may be denied.",
                        "Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    statusLabel.Text = "Scan failed";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during scan: {ex.Message}",
                    "Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Scan error";
            }
            finally
            {
                scanButton.Enabled = true;
                importButton.Enabled = true;
            }
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            using (var openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*";
                openDialog.Title = "Import Disk Space Data";

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string jsonText = File.ReadAllText(openDialog.FileName);
                        rootNode = FileSystemScanner.LoadFromJson(jsonText);
                        LoadData(rootNode);
                        statusLabel.Text = $"Imported: {FileSystemNode.FormatBytes(rootNode.Size)}";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import JSON: {ex.Message}",
                            "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            if (currentNode == null) return;

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "JSON Files (*.json)|*.json";
                saveDialog.FileName = $"disk-analysis-{DateTime.Now:yyyyMMdd-HHmmss}.json";
                saveDialog.Title = "Export Disk Space Data";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = FileSystemScanner.SaveToJson(currentNode);
                        File.WriteAllText(saveDialog.FileName, json);
                        statusLabel.Text = "Export complete";
                        MessageBox.Show("Data exported successfully!", "Export",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export: {ex.Message}",
                            "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportToPng_Click(object sender, EventArgs e)
        {
            if (currentNode == null) return;

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "PNG Image (*.png)|*.png";
                saveDialog.FileName = $"disk-treemap-{DateTime.Now:yyyyMMdd-HHmmss}.png";
                saveDialog.Title = "Export Treemap as Image";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Capture the treemap panel as a bitmap
                        Bitmap bmp = new Bitmap(treeMapPanel.Width, treeMapPanel.Height);
                        treeMapPanel.DrawToBitmap(bmp, new Rectangle(0, 0, treeMapPanel.Width, treeMapPanel.Height));
                        bmp.Save(saveDialog.FileName, System.Drawing.Imaging.ImageFormat.Png);

                        statusLabel.Text = "PNG exported";
                        MessageBox.Show("Treemap image exported successfully!", "Export",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export PNG: {ex.Message}",
                            "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ExportToExcel_Click(object sender, EventArgs e)
        {
            if (currentNode == null) return;

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "CSV File (Excel-Compatible) (*.csv)|*.csv";
                saveDialog.FileName = $"disk-analysis-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
                saveDialog.Title = "Export to CSV (Excel-Compatible)";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Generate Excel-friendly CSV
                        var sb = new System.Text.StringBuilder();

                        // Header row
                        sb.AppendLine("Path,Type,Size (Bytes),Size (GB),% of Total");

                        long totalSize = currentNode.Size;
                        FlattenForCsv(currentNode, sb, totalSize);

                        File.WriteAllText(saveDialog.FileName, sb.ToString());

                        int itemCount = sb.ToString().Split('\n').Length - 2;
                        statusLabel.Text = "CSV exported";

                        var result = MessageBox.Show($"Data exported to CSV successfully!\n\nTotal items: {itemCount:N0}\n\nWould you like to open it in Excel?",
                            "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(saveDialog.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export CSV: {ex.Message}",
                            "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void FlattenForCsv(FileSystemNode node, System.Text.StringBuilder sb, long totalSize)
        {
            if (node == null) return;

            // Add current node with Excel-friendly numbers
            double percentage = totalSize > 0 ? (double)node.Size / totalSize * 100.0 : 0;
            double sizeGB = node.Size / (1024.0 * 1024.0 * 1024.0);
            string path = node.Path?.Replace("\"", "\"\"") ?? ""; // Escape quotes

            // Format numbers as plain numbers (not formatted text) for Excel
            sb.AppendLine($"\"{path}\",{node.Type},{node.Size},{sizeGB:F3},{percentage:F2}");

            // Recursively add children
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children.OrderByDescending(c => c.Size))
                {
                    FlattenForCsv(child, sb, totalSize);
                }
            }
        }

        private void ExportToHtml_Click(object sender, EventArgs e)
        {
            if (currentNode == null) return;

            using (var saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "HTML File (*.html)|*.html";
                saveDialog.FileName = $"disk-analysis-{DateTime.Now:yyyyMMdd-HHmmss}.html";
                saveDialog.Title = "Export Interactive HTML Report";

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Capture treemap as base64 image
                        Bitmap bmp = new Bitmap(treeMapPanel.Width, treeMapPanel.Height);
                        treeMapPanel.DrawToBitmap(bmp, new Rectangle(0, 0, treeMapPanel.Width, treeMapPanel.Height));

                        string base64Image;
                        using (var ms = new System.IO.MemoryStream())
                        {
                            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            base64Image = Convert.ToBase64String(ms.ToArray());
                        }

                        // Generate HTML
                        var html = GenerateHtmlReport(currentNode, base64Image);
                        File.WriteAllText(saveDialog.FileName, html);

                        statusLabel.Text = "HTML exported";

                        var result = MessageBox.Show("HTML report exported successfully!\n\nWould you like to open it in your browser?",
                            "Export Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                        if (result == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(saveDialog.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to export HTML: {ex.Message}",
                            "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private string GenerateHtmlReport(FileSystemNode node, string base64Image)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine($"    <title>Disk Space Analysis - {node.Name}</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 20px; background: #0f172a; color: #e2e8f0; }");
            sb.AppendLine("        .container { max-width: 1400px; margin: 0 auto; }");
            sb.AppendLine("        h1 { color: #38bdf8; border-bottom: 3px solid #0ea5e9; padding-bottom: 10px; }");
            sb.AppendLine("        .summary { background: #1e293b; padding: 20px; border-radius: 8px; margin: 20px 0; }");
            sb.AppendLine("        .summary-item { display: inline-block; margin: 10px 30px 10px 0; }");
            sb.AppendLine("        .summary-label { color: #94a3b8; font-size: 14px; }");
            sb.AppendLine("        .summary-value { color: #38bdf8; font-size: 24px; font-weight: bold; }");
            sb.AppendLine("        .treemap { background: #1e293b; padding: 20px; border-radius: 8px; text-align: center; margin: 20px 0; }");
            sb.AppendLine("        .treemap img { max-width: 100%; border-radius: 4px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; background: #1e293b; border-radius: 8px; overflow: hidden; }");
            sb.AppendLine("        th { background: #334155; color: #38bdf8; padding: 12px; text-align: left; font-weight: 600; }");
            sb.AppendLine("        td { padding: 10px 12px; border-top: 1px solid #334155; }");
            sb.AppendLine("        tr:hover { background: #334155; }");
            sb.AppendLine("        .footer { text-align: center; color: #64748b; margin-top: 40px; padding: 20px; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class='container'>");
            sb.AppendLine($"        <h1>ðŸ“Š Disk Space Analysis Report</h1>");

            // Summary
            sb.AppendLine("        <div class='summary'>");
            sb.AppendLine($"            <div class='summary-item'><div class='summary-label'>Path</div><div class='summary-value'>{node.Path}</div></div>");
            sb.AppendLine($"            <div class='summary-item'><div class='summary-label'>Total Size</div><div class='summary-value'>{FileSystemNode.FormatBytes(node.Size)}</div></div>");
            sb.AppendLine($"            <div class='summary-item'><div class='summary-label'>Total Items</div><div class='summary-value'>{CountAllNodes(node):N0}</div></div>");
            sb.AppendLine($"            <div class='summary-item'><div class='summary-label'>Generated</div><div class='summary-value'>{DateTime.Now:yyyy-MM-dd HH:mm}</div></div>");
            sb.AppendLine("        </div>");

            // Treemap
            sb.AppendLine("        <div class='treemap'>");
            sb.AppendLine("            <h2>ðŸ“ˆ Visual Treemap</h2>");
            sb.AppendLine($"            <img src='data:image/png;base64,{base64Image}' alt='Disk Space Treemap'>");
            sb.AppendLine("        </div>");

            // Top 20 largest items
            sb.AppendLine("        <h2>ðŸ“‚ Top 20 Largest Items</h2>");
            sb.AppendLine("        <table>");
            sb.AppendLine("            <tr><th>Path</th><th>Type</th><th>Size</th><th>% of Total</th></tr>");

            var allNodes = new List<FileSystemNode>();
            FlattenAllNodes(node, allNodes);
            var top20 = allNodes.OrderByDescending(n => n.Size).Take(20);

            foreach (var item in top20)
            {
                double percentage = node.Size > 0 ? (double)item.Size / node.Size * 100.0 : 0;
                string icon = item.Type == "folder" ? "ðŸ“" : "ðŸ“„";
                sb.AppendLine($"            <tr><td>{icon} {item.Path}</td><td>{item.Type}</td><td>{FileSystemNode.FormatBytes(item.Size)}</td><td>{percentage:F2}%</td></tr>");
            }

            sb.AppendLine("        </table>");

            // Footer
            sb.AppendLine("        <div class='footer'>");
            sb.AppendLine("            Generated by <strong>Disk Space Analyzer - MSP Tool</strong>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private int CountAllNodes(FileSystemNode node)
        {
            if (node == null) return 0;
            int count = 1;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    count += CountAllNodes(child);
                }
            }
            return count;
        }

        private void FlattenAllNodes(FileSystemNode node, List<FileSystemNode> list)
        {
            if (node == null) return;
            list.Add(node);
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    FlattenAllNodes(child, list);
                }
            }
        }

        private void HomeButton_Click(object sender, EventArgs e)
        {
            if (rootNode != null)
            {
                navigationHistory.Clear();
                LoadData(rootNode);
            }
        }

        private void BackButton_Click(object sender, EventArgs e)
        {
            if (navigationHistory.Count > 0)
            {
                currentNode = navigationHistory.Pop();
                UpdateView();
            }
        }

        private void FilterButton_Click(object sender, EventArgs e)
        {
            filterPanel.Visible = !filterPanel.Visible;
        }

        private void FilterTextBox_TextChanged(object sender, EventArgs e)
        {
            if (currentNode != null)
            {
                treeMapPanel.SetData(currentNode, filterTextBox.Text);
            }
        }

        private void TreeMapPanel_NodeClicked(object sender, FileSystemNode node)
        {
            if (node.Children.Count > 0)
            {
                navigationHistory.Push(currentNode);
                currentNode = node;
                UpdateView();
            }
        }

        private void LoadData(FileSystemNode node)
        {
            navigationHistory.Clear();
            currentNode = node;
            rootNode = node;
            UpdateView();

            exportButton.Enabled = true;
            homeButton.Enabled = true;
            filterButton.Enabled = true;

            // Enable zoom buttons
            var buttons = (dynamic)this.Tag;
            if (buttons != null)
            {
                buttons.ZoomOut.Enabled = true;
                buttons.ZoomIn.Enabled = true;
                UpdateZoomButtons(buttons.ZoomOut, buttons.ZoomIn);
            }
        }

        private void UpdateView()
        {
            treeMapPanel.SetData(currentNode, filterTextBox.Text);

            // Calculate visible items for debug
            int visibleCount = currentNode.GetVisibleChildren(filterTextBox.Text).Count;

            breadcrumbLabel.Text = $"{currentNode.Path} â€¢ {FileSystemNode.FormatBytes(currentNode.Size)} â€¢ {currentNode.Children.Count} items";
            statusLabel.Text = $"Displaying {visibleCount} items (Depth: {treeMapPanel.ZoomLevel})";
            backButton.Enabled = navigationHistory.Count > 0;
        }

        private void UpdateZoomButtons(dynamic zoomOut, dynamic zoomIn)
        {
            zoomOut.Enabled = treeMapPanel.ZoomLevel > 1;
            zoomIn.Enabled = treeMapPanel.ZoomLevel < 5;
        }

        private void UpdateSmartStatus(string path)
        {
            try
            {
                // Get drive letter from path
                string driveLetter = System.IO.Path.GetPathRoot(path)?.TrimEnd('\\');
                if (string.IsNullOrEmpty(driveLetter))
                {
                    driveLetter = "C"; // Default to C: if can't determine
                }

                // Find the SMART status label - search all controls recursively
                Label smartLabel = null;
                foreach (Control control in this.Controls)
                {
                    if (control is Panel panel)
                    {
                        foreach (Control child in panel.Controls)
                        {
                            if (child is Label label && label.Name == "smartStatusLabel")
                            {
                                smartLabel = label;
                                break;
                            }
                        }
                    }
                    if (smartLabel != null) break;
                }

                if (smartLabel == null)
                    return; // Label not found, silently skip

                // Use WMI to check SMART status
                string driveQuery = driveLetter.Replace(":", "");
                var status = GetDriveSmartStatus(driveQuery);

                switch (status)
                {
                    case SmartStatus.Healthy:
                        smartLabel.Text = "Drive Health: ✓ Good";
                        smartLabel.ForeColor = Color.FromArgb(34, 197, 94); // green
                        break;
                    case SmartStatus.Warning:
                        smartLabel.Text = "Drive Health: ⚠  Warning";
                        smartLabel.ForeColor = Color.FromArgb(234, 179, 8); // yellow
                        break;
                    case SmartStatus.Failing:
                        smartLabel.Text = "Drive Health: ✗ Failing";
                        smartLabel.ForeColor = Color.FromArgb(220, 38, 38); // red
                        break;
                    default:
                        smartLabel.Text = ""; // Don't show anything if unknown
                        smartLabel.ForeColor = Color.FromArgb(148, 163, 184); // gray
                        break;
                }

                smartLabel.Visible = true;
            }
            catch
            {
                // Silently fail - SMART not critical
            }
        }

        private enum SmartStatus
        {
            Unknown,
            Healthy,
            Warning,
            Failing
        }

        private SmartStatus GetDriveSmartStatus(string driveLetter)
        {
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Model, DeviceID, Status FROM Win32_DiskDrive"))
                {
                    var collection = searcher.Get();

                    bool hasOK = false;
                    bool hasWarning = false;

                    foreach (System.Management.ManagementObject drive in collection)
                    {
                        var status = drive["Status"]?.ToString();

                        if (status != null && status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                        {
                            hasOK = true;
                        }
                        else if (status != null && (status.Contains("Degraded") || status.Contains("Error")))
                        {
                            hasWarning = true;
                        }
                    }

                    if (hasWarning) return SmartStatus.Warning;
                    if (hasOK) return SmartStatus.Healthy;
                    if (collection.Count > 0) return SmartStatus.Healthy; // Drives found, assume OK
                }

                return SmartStatus.Unknown;
            }
            catch
            {
                // Silently fail - SMART is not critical
                return SmartStatus.Unknown;
            }
        }
    }
}