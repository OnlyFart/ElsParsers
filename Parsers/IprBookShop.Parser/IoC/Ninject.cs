using Core.Configs;
using Core.IoC;
using Core.Providers.Implementations;
using Core.Providers.Interfaces;
using Core.Types;
using IprBookShop.Parser.Configs;
using Parser.Core.Configs;

namespace IprBookShop.Parser.IoC {
    public class Ninject : CoreNinjectModule {
        private readonly Options _options;

        public Ninject(Options options) {
            _options = options;
        }

        public override void Load() {
            base.Load();

            Bind<IMongoConfig>().ToConstant((IMongoConfig) _options);
            Bind<IParserConfigBase>().ToConstant((IParserConfig) _options);
            Bind<IRepository<BookInfo>>().To<MongoRepository<BookInfo>>();
        }
    }
}
