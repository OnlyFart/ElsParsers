using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Book.Comparer.Logic.SimilarSaver;
using Book.Comparer.Types;

namespace Sandbox {
    public class Saver : ISimilarSaver {
        public async Task Save(SaveResult saveResult) {
            var lines = new List<string>();
            
            lines.Add(saveResult.Book.Bib);
            
            lines.Add("Авторы: " + saveResult.Book.Authors);
            lines.Add("Название: " + saveResult.Book.Name);
            lines.Add("Издательство: " + saveResult.Book.Publisher);
            lines.AddRange(saveResult.SimilarBooks.Select(simBook => simBook.Bib));
            lines.Add(string.Empty);
                
            await File.AppendAllLinesAsync("similar.txt", lines, Encoding.UTF8);
        }
    }
}
