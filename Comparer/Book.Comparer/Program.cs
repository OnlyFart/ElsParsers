using System;
using System.Threading.Tasks;
using Book.Comparer.Logic.Comparers;
using Core.Configs;
using Core.Providers.Implementations;
using Core.Types;

namespace Book.Comparer {
   
    public class MongoConfig : IMongoConfig {
        public string ConnectionString => "mongodb://localhost:27017/?serverSelectionTimeoutMS=5000&connectTimeoutMS=10000&3t.uriVersion=3&3t.connection.name=localhost&3t.alwaysShowAuthDB=true&3t.alwaysShowDBFromUserRole=true";
        public string DatabaseName => "ELS";
        public string CollectionName => "Books";
    }
    
    class Program {
        static async Task Main(string[] args) {
            var repository = new MongoRepository<BookInfo>(new MongoConfig());

            var comparer = new Logic.Comparer(repository, new BookComparer(0.3m, 0.4m));

            await comparer.Run();
            Console.WriteLine("Hello World!");
        }
    }
}
