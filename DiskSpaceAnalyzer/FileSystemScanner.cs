using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace DiskSpaceAnalyzer
{
    public class FileSystemScanner
    {
        public event EventHandler<string> StatusChanged;
        private int maxDepth;
        private int foldersScanned = 0;
        private System.Threading.Timer progressTimer;

        public FileSystemScanner(int maxDepth = 5)
        {
            this.maxDepth = maxDepth;
        }

        public async Task<FileSystemNode> ScanAsync(string path)
        {
            foldersScanned = 0;

            // Update status every 500ms with folder count
            progressTimer = new System.Threading.Timer(_ =>
            {
                int count = System.Threading.Interlocked.CompareExchange(ref foldersScanned, 0, 0);
                StatusChanged?.Invoke(this, $"Scanning... {count:N0} folders processed");
            }, null, 0, 500);

            var result = await Task.Run(() => Scan(path, 0));

            progressTimer?.Dispose();
            StatusChanged?.Invoke(this, $"Scan complete: {foldersScanned:N0} folders");

            return result;
        }

        private FileSystemNode Scan(string path, int currentDepth)
        {
            try
            {
                // Don't fire StatusChanged from worker threads - causes deadlock in WinForms
                // StatusChanged?.Invoke(this, $"Scanning: {path}");

                DirectoryInfo dirInfo = new DirectoryInfo(path);

                // Increment folders scanned counter (thread-safe)
                System.Threading.Interlocked.Increment(ref foldersScanned);

                FileSystemNode node = new FileSystemNode
                {
                    Name = dirInfo.Name,
                    Path = path,
                    Type = "folder",
                    Children = new List<FileSystemNode>()
                };

                long totalSize = 0;
                int filesAdded = 0;
                int filesSkipped = 0;

                // Scan files in current directory
                try
                {
                    var files = dirInfo.GetFiles();
                    foreach (var file in files)
                    {
                        try
                        {
                            var fileNode = new FileSystemNode
                            {
                                Name = file.Name,
                                Path = file.FullName,
                                Type = "file",
                                Size = file.Length,
                                Children = new List<FileSystemNode>()
                            };
                            node.Children.Add(fileNode);
                            totalSize += file.Length;
                            filesAdded++;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            filesSkipped++;
                        }
                        catch (Exception)
                        {
                            filesSkipped++;
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"ACCESS DENIED listing files in: {path}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR listing files in {path}: {ex.Message}");
                }

                // Scan subdirectories IN PARALLEL
                int foldersAdded = 0;
                int foldersSkipped = 0;
                int foldersSkippedReparse = 0;

                try
                {
                    var directories = dirInfo.GetDirectories();

                    // Use thread-safe collection for parallel results
                    var childNodes = new ConcurrentBag<FileSystemNode>();

                    // Parallel scan subdirectories (will use multiple CPU cores!)
                    Parallel.ForEach(directories, new ParallelOptions { MaxDegreeOfParallelism = 4 }, dir =>
                    {
                        try
                        {
                            // Skip system junctions and reparse points to avoid infinite loops
                            if ((dir.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                            {
                                System.Threading.Interlocked.Increment(ref foldersSkippedReparse);
                                return;
                            }

                            var childNode = Scan(dir.FullName, currentDepth + 1);
                            if (childNode != null && childNode.Size > 0)
                            {
                                childNodes.Add(childNode);
                                System.Threading.Interlocked.Increment(ref foldersAdded);
                            }
                            else if (childNode != null && childNode.Size == 0)
                            {
                                System.Threading.Interlocked.Increment(ref foldersSkipped);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            System.Threading.Interlocked.Increment(ref foldersSkipped);
                            System.Diagnostics.Debug.WriteLine($"ACCESS DENIED to folder: {dir.FullName}");
                        }
                        catch (Exception ex)
                        {
                            System.Threading.Interlocked.Increment(ref foldersSkipped);
                            System.Diagnostics.Debug.WriteLine($"ERROR scanning folder {dir.FullName}: {ex.Message}");
                        }
                    });

                    // Add all child nodes and calculate total size
                    foreach (var childNode in childNodes)
                    {
                        node.Children.Add(childNode);
                        totalSize += childNode.Size;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"ACCESS DENIED listing subdirectories in: {path}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR listing subdirectories in {path}: {ex.Message}");
                }

                node.Size = totalSize;

                System.Diagnostics.Debug.WriteLine($"COMPLETED: {path} - Size: {FileSystemNode.FormatBytes(totalSize)}, Files: {filesAdded}, Folders: {foldersAdded}, Children: {node.Children.Count}");

                return node;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FATAL ERROR scanning {path}: {ex.Message}");
                return null;
            }
        }

        public static FileSystemNode LoadFromJson(string jsonText)
        {
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                var dict = serializer.Deserialize<Dictionary<string, object>>(jsonText);
                return ParseNode(dict);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to parse JSON: " + ex.Message);
            }
        }

        private static FileSystemNode ParseNode(Dictionary<string, object> dict)
        {
            var node = new FileSystemNode
            {
                Name = dict.ContainsKey("name") ? dict["name"].ToString() : "",
                Path = dict.ContainsKey("path") ? dict["path"].ToString() : "",
                Type = dict.ContainsKey("type") ? dict["type"].ToString() : "folder"
            };

            if (dict.ContainsKey("size"))
            {
                if (dict["size"] is int)
                    node.Size = (int)dict["size"];
                else if (dict["size"] is long)
                    node.Size = (long)dict["size"];
                else
                    node.Size = Convert.ToInt64(dict["size"]);
            }

            if (dict.ContainsKey("children") && dict["children"] is object[])
            {
                var children = (object[])dict["children"];
                foreach (var child in children)
                {
                    if (child is Dictionary<string, object>)
                    {
                        node.Children.Add(ParseNode((Dictionary<string, object>)child));
                    }
                }
            }

            return node;
        }

        public static string SaveToJson(FileSystemNode node)
        {
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            return serializer.Serialize(ConvertToDict(node));
        }

        private static Dictionary<string, object> ConvertToDict(FileSystemNode node)
        {
            var dict = new Dictionary<string, object>
            {
                ["name"] = node.Name,
                ["path"] = node.Path,
                ["size"] = node.Size,
                ["type"] = node.Type
            };

            if (node.Children.Count > 0)
            {
                dict["children"] = node.Children.Select(c => ConvertToDict(c)).ToArray();
            }

            return dict;
        }
    }
}