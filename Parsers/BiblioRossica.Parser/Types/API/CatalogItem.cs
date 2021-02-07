namespace BiblioRossica.Parser.Types.API {
    public class CatalogItem {
        public string DirectoryId;
        public string Name;

        public CatalogItem(string directoryId, string name) {
            DirectoryId = directoryId;
            Name = name;
        }
    }
}
