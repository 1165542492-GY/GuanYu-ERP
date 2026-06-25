using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace SupplierErpApp
{
    public class RestoreBackupRequest
    {
        public string BackupName { get; set; }
    }

    public static partial class Program
    {
        static readonly HashSet<string> DataBackupExcludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".dll", ".exe", ".pdb", ".sln", ".html", ".js", ".css"
        };

        static readonly HashSet<string> DataBackupExcludedDirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", ".git"
        };

        static bool ShouldIncludeDataBackupFile(string filePath)
        {
            var name = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith(".")) return false;
            var ext = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(ext) && DataBackupExcludedExtensions.Contains(ext)) return false;
            return true;
        }

        static IEnumerable<string> EnumerateDataBackupFiles(string rootDir)
        {
            if (!Directory.Exists(rootDir)) yield break;
            foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(rootDir, file);
                if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("..")) continue;
                var parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Any(p => DataBackupExcludedDirNames.Contains(p))) continue;
                if (!ShouldIncludeDataBackupFile(file)) continue;
                yield return file;
            }
        }

        static void CopyDataDirectoryTo(string destFolder)
        {
            Directory.CreateDirectory(destFolder);
            foreach (var src in EnumerateDataBackupFiles(DataDir))
            {
                var relative = Path.GetRelativePath(DataDir, src);
                var dest = Path.Combine(destFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(src, dest, true);
            }
        }

        static void CopyBackupFolderToData(string sourceFolder)
        {
            Directory.CreateDirectory(DataDir);
            foreach (var src in EnumerateDataBackupFiles(sourceFolder))
            {
                var relative = Path.GetRelativePath(sourceFolder, src);
                var dest = Path.Combine(DataDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(src, dest, true);
            }
        }

        static void ClearDataDirectory()
        {
            if (!Directory.Exists(DataDir)) return;
            foreach (var file in Directory.EnumerateFiles(DataDir, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(DataDir, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
            {
                try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir); } catch { }
            }
        }

        static object BuildBackupFolderInfo(string folderPath)
        {
            var dir = new DirectoryInfo(folderPath);
            long size = 0;
            int count = 0;
            if (dir.Exists)
            {
                foreach (var file in EnumerateDataBackupFiles(folderPath))
                {
                    count++;
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            return new
            {
                name = dir.Name,
                createdAt = dir.Exists ? dir.CreationTime.ToString("yyyy-MM-dd HH:mm:ss") : "",
                fileCount = count,
                size = size,
                sizeText = FormatByteSize(size),
                path = folderPath
            };
        }

        static string FormatByteSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return Math.Round(kb, 2) + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return Math.Round(mb, 2) + " MB";
            return Math.Round(mb / 1024.0, 2) + " GB";
        }

        static bool TryResolveBackupFolder(string backupName, out string folderPath, out string error)
        {
            folderPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(backupName))
            {
                error = "请指定备份名称";
                return false;
            }
            backupName = backupName.Trim();
            if (backupName.Contains("..") || backupName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || backupName.Contains('/') || backupName.Contains('\\'))
            {
                error = "备份名称无效，禁止路径穿越";
                return false;
            }
            folderPath = Path.GetFullPath(Path.Combine(BackupDir, backupName));
            var backupRoot = Path.GetFullPath(BackupDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!folderPath.StartsWith(backupRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = "备份路径无效";
                return false;
            }
            if (!Directory.Exists(folderPath))
            {
                error = "备份不存在：" + backupName;
                return false;
            }
            return true;
        }

        static string CreateFullDataBackup(string folderPrefix)
        {
            Directory.CreateDirectory(BackupDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string folder = Path.Combine(BackupDir, folderPrefix + stamp);
            CopyDataDirectoryTo(folder);
            return folder;
        }

        static void CreateDataBackup(HttpListenerContext ctx, UserSession user)
        {
            if (!RequireAdmin(ctx, user)) return;
            try
            {
                string folder;
                object info;
                lock (DataLock)
                {
                    folder = CreateFullDataBackup("Backup_");
                    info = BuildBackupFolderInfo(folder);
                }
                Audit(user, "创建数据备份", Path.GetFileName(folder));
                var data = (dynamic)info;
                WriteJson(ctx, new
                {
                    ok = true,
                    message = "备份完成",
                    name = data.name,
                    path = data.path,
                    createdAt = data.createdAt,
                    fileCount = data.fileCount,
                    size = data.size,
                    sizeText = data.sizeText
                });
            }
            catch (Exception ex)
            {
                WriteJson(ctx, new { error = "备份失败：" + ToUserMessage(ex), message = "备份失败：" + ToUserMessage(ex) }, 500);
            }
        }

        static void ListDataBackups(HttpListenerContext ctx, UserSession user)
        {
            if (!RequireAdmin(ctx, user)) return;
            try
            {
                Directory.CreateDirectory(BackupDir);
                var items = new DirectoryInfo(BackupDir).GetDirectories()
                    .OrderByDescending(x => x.CreationTime)
                    .Select(x => BuildBackupFolderInfo(x.FullName))
                    .ToArray();
                WriteJson(ctx, new { items, dataDir = DataDir, backupDir = BackupDir });
            }
            catch (Exception ex)
            {
                WriteJson(ctx, new { error = "读取备份列表失败：" + ToUserMessage(ex) }, 500);
            }
        }

        static void RestoreDataBackup(HttpListenerContext ctx, UserSession user)
        {
            if (!RequireAdmin(ctx, user)) return;
            var req = Json.Deserialize<RestoreBackupRequest>(ReadBody(ctx.Request));
            string backupName = req == null ? null : req.BackupName;
            string sourceFolder;
            string error;
            if (!TryResolveBackupFolder(backupName, out sourceFolder, out error))
            {
                WriteJson(ctx, new { error = error, message = error }, 400);
                return;
            }
            string beforeRestoreFolder = null;
            bool dataCleared = false;
            try
            {
                lock (DataLock)
                {
                    beforeRestoreFolder = CreateFullDataBackup("BeforeRestore_");
                    ClearDataDirectory();
                    dataCleared = true;
                    CopyBackupFolderToData(sourceFolder);
                }
                Audit(user, "恢复数据", backupName);
                WriteJson(ctx, new
                {
                    ok = true,
                    message = "备份恢复成功。请关闭并重新启动冠誉制造ERP.exe，然后在浏览器按 Ctrl+F5 强制刷新。",
                    backupName = backupName,
                    beforeRestoreFolder = Path.GetFileName(beforeRestoreFolder),
                    beforeRestorePath = beforeRestoreFolder
                });
            }
            catch (Exception ex)
            {
                if (dataCleared && !string.IsNullOrWhiteSpace(beforeRestoreFolder) && Directory.Exists(beforeRestoreFolder))
                {
                    try
                    {
                        lock (DataLock)
                        {
                            ClearDataDirectory();
                            CopyBackupFolderToData(beforeRestoreFolder);
                        }
                    }
                    catch { }
                }
                string msg = "恢复失败：" + ToUserMessage(ex);
                if (!string.IsNullOrWhiteSpace(beforeRestoreFolder))
                    msg += "。已保留恢复前自动备份：" + Path.GetFileName(beforeRestoreFolder);
                WriteJson(ctx, new
                {
                    error = msg,
                    message = msg,
                    beforeRestoreFolder = beforeRestoreFolder == null ? null : Path.GetFileName(beforeRestoreFolder),
                    beforeRestorePath = beforeRestoreFolder
                }, 500);
            }
        }

        static void OpenBackupDirectory(HttpListenerContext ctx, UserSession user, bool openDataDir)
        {
            if (!RequireAdmin(ctx, user)) return;
            string target = openDataDir ? DataDir : BackupDir;
            try
            {
                Directory.CreateDirectory(target);
                Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
                WriteJson(ctx, new { ok = true, path = target, message = "已打开目录" });
            }
            catch (Exception ex)
            {
                WriteJson(ctx, new
                {
                    ok = false,
                    path = target,
                    message = "无法自动打开目录，请手动打开：" + target,
                    error = ToUserMessage(ex)
                });
            }
        }
    }
}
