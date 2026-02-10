using System;
using System.Collections.Generic;
using System.Linq;

namespace DiskSpaceAnalyzer
{
    public class FileSystemNode
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string Type { get; set; }
        public List<FileSystemNode> Children { get; set; }

        public FileSystemNode()
        {
            Children = new List<FileSystemNode>();
        }

        public double GetPercentage(long totalSize)
        {
            if (totalSize == 0) return 0;
            return (double)Size / totalSize * 100.0;
        }

        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return String.Format("{0:0.##} {1}", len, sizes[order]);
        }

        public List<FileSystemNode> GetVisibleChildren(string filterText)
        {
            if (string.IsNullOrEmpty(filterText))
                return Children;

            // Split by comma to support multiple filters
            var filters = filterText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(f => f.Trim())
                                     .Where(f => !string.IsNullOrEmpty(f))
                                     .ToList();

            if (filters.Count == 0)
                return Children;

            // Separate include and exclude filters
            var includeFilters = filters.Where(f => !f.StartsWith("!")).ToList();
            var excludeFilters = filters.Where(f => f.StartsWith("!")).Select(f => f.Substring(1).Trim()).ToList();

            return Children.Where(c =>
            {
                string name = c.Name ?? "";
                string path = c.Path ?? "";

                // Check exclude filters first - if any match, hide the item
                foreach (var exclude in excludeFilters)
                {
                    if (!string.IsNullOrEmpty(exclude) &&
                        (name.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return false; // Hide this item
                    }
                }

                // If there are include filters, check if item or descendants match
                if (includeFilters.Count > 0)
                {
                    // Check if THIS item matches
                    bool thisMatches = false;
                    foreach (var include in includeFilters)
                    {
                        if (!string.IsNullOrEmpty(include) &&
                            (name.IndexOf(include, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             path.IndexOf(include, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            thisMatches = true;
                            break;
                        }
                    }
                    
                    if (thisMatches)
                        return true; // Show it
                    
                    // If this doesn't match, check if any DESCENDANTS match (recursive)
                    if (c.Type == "folder" && ContainsMatchingDescendant(c, includeFilters))
                        return true; // Show folder because it contains matching items
                    
                    return false; // Neither this nor descendants match
                }

                return true; // Show this item
            }).ToList();
        }

        private bool ContainsMatchingDescendant(FileSystemNode node, List<string> includeFilters)
        {
            if (node.Children == null || node.Children.Count == 0)
                return false;

            foreach (var child in node.Children)
            {
                string name = child.Name ?? "";
                string path = child.Path ?? "";

                // Check if child matches
                foreach (var include in includeFilters)
                {
                    if (!string.IsNullOrEmpty(include) &&
                        (name.IndexOf(include, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf(include, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true; // Found a match!
                    }
                }

                // Recurse into child folders
                if (child.Type == "folder" && ContainsMatchingDescendant(child, includeFilters))
                    return true;
            }

            return false;
        }
    }
}
