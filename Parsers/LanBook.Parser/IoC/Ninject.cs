using Parser.Core;
using Core.Configs;
using Core.IoC;
using Core.Providers.Implementations;
using Core.Providers.Interfaces;
using LanBook.Parser.Configs;

namespace LanBook.Parser.IoC {
    public class Ninject : CoreNinjectModule {
        private readonly Options _options;

        public Ninject(Options options) {
            _options = options;
        }

        public override void Load() {
            base.Load();

            Bind<IMongoConfig>().ToConstant((IMongoConfig) _options);
            Bind<IParserConfig>().ToConstant((IParserConfig) _options);
            Bind<IRepository<Book>>().To<MongoRepository<Book>>();
        }
    }
}