using Book.Comparer.Configs;
using Book.Comparer.Logic.Comparers;
using Book.Comparer.Logic.Configs;
using Core.Configs;
using Core.Providers.Implementations;
using Core.Providers.Interfaces;
using Core.Types;
using Ninject.Modules;

namespace Book.Comparer.IoC {
    public class Ninject : NinjectModule {
        private readonly Options _options;

        public Ninject(Options options) {
            _options = options;
        }

        public override void Load() {
            Bind<IMongoConfig>().ToConstant((IMongoConfig) _options);
            Bind<IComparerConfig>().ToConstant((IComparerConfig) _options);
            Bind<IBookComparer>().To<BookComparer>();
            Bind<IBookComparerConfig>().ToConstant((IBookComparerConfig) _options);
            Bind<IRepository<BookInfo>>().To<MongoRepository<BookInfo>>();
        }
    }
}
