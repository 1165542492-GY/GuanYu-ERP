using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SupplierErpApp
{
    public static partial class Program
    {
        struct JsonMutationResult<TResult>
        {
            public TResult Value;
            public bool Save;
            public JsonMutationResult(TResult value, bool save) { Value = value; Save = save; }
        }

        static List<T> ReadJsonListCore<T>(string file)
        {
            if (!File.Exists(file)) return new List<T>();
            return Json.Deserialize<List<T>>(File.ReadAllText(file, Encoding.UTF8)) ?? new List<T>();
        }

        static void WriteJsonListCore<T>(string file, string backupPrefix, List<T> items)
        {
            string temp = file + ".tmp";
            File.WriteAllText(temp, Json.Serialize(items), new UTF8Encoding(false));
            if (File.Exists(file))
            {
                string backup = Path.Combine(BackupDir, backupPrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".json");
                File.Replace(temp, file, backup);
            }
            else File.Move(temp, file);
            CleanBackups();
        }

        /// <summary>在同一 DataLock 内完成读-改-写，避免并发丢失更新。</summary>
        static TResult MutateJsonList<T, TResult>(string file, string backupPrefix, Func<List<T>, JsonMutationResult<TResult>> mutator)
        {
            lock (DataLock)
            {
                var list = ReadJsonListCore<T>(file);
                var result = mutator(list);
                if (result.Save) WriteJsonListCore(file, backupPrefix, list);
                return result.Value;
            }
        }

        /// <summary>在同一 DataLock 内完成读-改-写（始终保存）。</summary>
        static void MutateJsonList<T>(string file, string backupPrefix, Action<List<T>> mutator)
        {
            lock (DataLock)
            {
                var list = ReadJsonListCore<T>(file);
                mutator(list);
                WriteJsonListCore(file, backupPrefix, list);
            }
        }

        static void RunUnderDataLock(Action action)
        {
            lock (DataLock) action();
        }
    }
}
