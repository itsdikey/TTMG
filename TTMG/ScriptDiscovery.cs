namespace TTMG
{
    public static class ScriptDiscovery
    {
        public static List<ScriptMetadata> Discover(string rootDir, AppConfig config)
        {
            var scripts = new List<ScriptMetadata>();
            var allFiles = Directory.GetFiles(rootDir, "*.lua", new EnumerationOptions 
            { 
                RecurseSubdirectories = true, 
                AttributesToSkip = 0 
            });

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file);
                var dir = Path.GetDirectoryName(file) ?? "";
                var dirName = Path.GetFileName(dir);
                
                string displayName;
                if (fileName.Equals("init.lua", StringComparison.OrdinalIgnoreCase))
                {
                    displayName = dirName;
                }
                else
                {
                    displayName = Path.GetFileNameWithoutExtension(fileName);
                }

                scripts.Add(new ScriptMetadata { DisplayName = displayName, FullPath = file });
            }

            // Recursive Disambiguation (Prepending folders)
            bool changed;
            do
            {
                changed = false;
                var groups = scripts.GroupBy(s => s.DisplayName).Where(g => g.Count() > 1);
                foreach (var group in groups)
                {
                    foreach (var item in group)
                    {
                        var relPath = Path.GetRelativePath(rootDir, item.FullPath);
                        var pathParts = relPath.Split(Path.DirectorySeparatorChar);
                        
                        var currentDisplayParts = item.DisplayName.Split('-');
                        int currentLevel = currentDisplayParts.Length;

                        bool isInit = Path.GetFileName(item.FullPath).Equals("init.lua", StringComparison.OrdinalIgnoreCase);
                        int levelsUsed = isInit ? currentLevel : currentLevel - 1;

                        if (pathParts.Length > levelsUsed + 1)
                        {
                            var parentDir = pathParts[pathParts.Length - 2 - levelsUsed];
                            item.DisplayName = $"{parentDir}-{item.DisplayName}";
                            changed = true;
                        }
                    }
                }
            } while (changed);

            // Map aliases from config
            foreach (var s in scripts)
            {
                var cfg = config.Scripts.FirstOrDefault(c => Path.GetFullPath(c.Path, rootDir) == Path.GetFullPath(s.FullPath, rootDir));
                if (cfg != null) s.Alias = cfg.Alias;
            }

            return scripts;
        }
    }
}
