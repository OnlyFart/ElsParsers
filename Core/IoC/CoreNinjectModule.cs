using System.Net;
using Ninject.Modules;

namespace Core.IoC {
    public class CoreNinjectModule : NinjectModule {
        public override void Load() {
            ServicePointManager.DefaultConnectionLimit = 1000;
        }
    }
}
