using Autofac;
using RiskClassificationLab.Services.Interfaces;

namespace RiskClassificationLab.Services.Autofac
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
        }
    }
}
