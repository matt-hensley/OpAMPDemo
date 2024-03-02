using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace OpAMPDemo
{
    public class LogCategoryProvider : ILoggerProvider
    {
        private ConcurrentDictionary<string, bool> _categories = new ConcurrentDictionary<string, bool>();

        public ILogger CreateLogger(string categoryName)
        {
            _categories[categoryName] = true;
            return NullLogger.Instance;
        }

        public void Dispose()
        {
        }

        public IEnumerable<string> GetCategories()
        {
            return _categories.Keys;
        }
    }
}
