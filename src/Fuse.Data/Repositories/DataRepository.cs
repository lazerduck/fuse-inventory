using Fuse.Core.Interfaces;
using Newtonsoft.Json;

namespace Fuse.Data.Repositories
{
    public class DataRepository : IDataRepository
    {
        private readonly string _root = "DataStore";

        public async Task SaveObjectAsync<T>(string name, T obj)
        {
            var path = Path.Combine(_root, name);
            if (!Directory.Exists(_root))
            {
                Directory.CreateDirectory(_root);
            }

            if (File.Exists(path))
            {
                File.Move(path, Path.Combine(_root, name + ".bak"), true);
            }

            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fileStream);
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            await writer.WriteAsync(json);
        }

        public async Task<T?> GetObjectAsync<T>(string name)
        {
            var path = Path.Combine(_root, name);
            if (!File.Exists(path))
            {
                return default;
            }

            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);
            var json = await reader.ReadToEndAsync();
            var obj = JsonConvert.DeserializeObject<T>(json);
            return obj;
        }
    }
}