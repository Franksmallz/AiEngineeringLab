
using Autofac;
using FoundationalModel.Core.Enums;
using FoundationalModel.Services.Implementations.Providers;
using FoundationalModel.Services.Interfaces;

namespace FoundationalModel.Services.Autofac
{
    public class AutofacContainerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(typeof(IAutoDependencyService).Assembly)
                .AssignableTo<IAutoDependencyService>()
                .As<IAutoDependencyService>()
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            builder.RegisterType<AnthropicProviderManagement>()
               .Keyed<IModelProvider>(Providers.ANTHROPIC).InstancePerLifetimeScope();
            builder.RegisterType<OpenAiProviderManagement>()
               .Keyed<IEmbeddingProvider>(EmbeddingProvider.OPENAI).InstancePerLifetimeScope();
        }
    }
}
