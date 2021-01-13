using BiblioclubParser.Configs;
using BiblioclubParser.Types;
using Core.Configs;
using Core.IoC;
using Core.Providers.Implementations;
using Core.Providers.Interfaces;

namespace BiblioclubParser.IoC {
    public class Ninject : CoreNinjectModule {
        private readonly Options _options;

        public Ninject(Options options) {
            _options = options;
        }
        
        public override void Load() {
            base.Load();

            Bind<IMongoConfig>().ToConstant((IMongoConfig)_options);
            Bind<IParserConfig>().ToConstant((IParserConfig) _options);
            Bind<IBooksProvider<Book>>().To<MongoBooksProvider<Book>>();
        }
    }
}
