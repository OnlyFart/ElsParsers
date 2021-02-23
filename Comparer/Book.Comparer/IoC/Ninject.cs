using Book.Comparer.Configs;
using Book.Comparer.Logic.BookGetter;
using Book.Comparer.Logic.Comparers;
using Book.Comparer.Logic.Configs;
using Book.Comparer.Logic.SimilarSaver;
using Core.Configs;
using Core.Providers.Implementations;
using Core.Providers.Interfaces;
using Core.Types;
using Microsoft.Extensions.Configuration;
using Ninject.Modules;

namespace Book.Comparer.IoC {
    public class Ninject : NinjectModule {
        private readonly Options _options;

        public Ninject(Options options) {
            _options = options;
        }

        public override void Load() {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true,true)
                .Build();

            var normalizerConfig = configuration.GetSection("NormalizerConfig").Get<NormalizerConfig>();

            Bind<NormalizerConfig>().ToConstant(normalizerConfig);
            Bind<IMongoConfig>().ToConstant((IMongoConfig) _options);
            Bind<IComparerConfig>().ToConstant((IComparerConfig) _options);
            Bind<IBookComparer>().To<BookComparer>();
            Bind<ISimilarSaver>().To<SimilarSaver>();
            Bind<ICompareBookGetter>().To<CompareBookGetter>();
            Bind<IBookComparerConfig>().ToConstant((IBookComparerConfig) _options);
            Bind<IRepository<BookInfo>>().To<MongoRepository<BookInfo>>();
        }
    }
}
