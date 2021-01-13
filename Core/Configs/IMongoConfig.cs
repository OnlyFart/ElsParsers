namespace Core.Configs {
    public interface IMongoConfig {
        /// <summary>
        /// Строка подключения в MongoDb
        /// </summary>
        public string ConnectionString { get; set; }
        
        /// <summary>
        /// Название базы данных
        /// </summary>
        public string DatabaseName { get; set; }
        
        /// <summary>
        /// Название коллекции
        /// </summary>
        public string CollectionName { get; set; }
    }
}
