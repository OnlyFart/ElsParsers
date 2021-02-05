using System.Net;
using Core.Configs;
using Core.Providers.Implementations;
using Core.Providers.Interfaces;
using Core.Types;
using Ninject.Modules;
using Parser.Core.Configs;

namespace Parser.Core.IoC {
    public class CoreNinjectModule : NinjectModule {
        private readonly OptionsBase _options;

        public CoreNinjectModule(OptionsBase options) {
            _options = options;
        }
        
        public override void Load() {
            ServicePointManager.DefaultConnectionLimit = 1000;
            
            Bind<IMongoConfig>().ToConstant((IMongoConfig) _options);
            Bind<IParserConfigBase>().ToConstant((IParserConfigBase) _options);
            Bind<IRepository<BookInfo>>().To<MongoRepository<BookInfo>>();
        }
    }
}
