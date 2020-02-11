namespace GenericSampleCode.DataParse.AppStart
{
    using GenericSampleCode.DataParse.Calculations;
    using GenericSampleCode.DataParse.FileReaders;
    using GenericSampleCode.DataParse.Models;
    using GenericSampleCode.DataParse.Resolvers;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using StructureMap;
    using System;

    public class Startup
    {
        public static IServiceProvider GetServiceProvider()
        {
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Trace));
            serviceCollection.AddSingleton<ICalculationService, CalculationService>();
            serviceCollection.AddSingleton(typeof(IFactory<>), typeof(Factory<>));
            serviceCollection.AddTransient<IFileReaderService<DataTypeA>, CSVFileReaderService<DataTypeA>>();
            serviceCollection.AddTransient<IFileReaderService<DataTypeB>, CSVFileReaderService<DataTypeB>>();
            serviceCollection.AddTransient<IFileReaderService<DataTypeA>, JSONFileReaderService<DataTypeA>>();
            serviceCollection.AddTransient<IFileReaderService<DataTypeB>, JSONFileReaderService<DataTypeB>>();

            //var container = new Container();
            //container.Configure(config =>
            //{
            //    // Register stuff in container, using the StructureMap APIs...
            //    config.For<IFileReaderService<DataTypeA>>().Add(new CSVFileReaderService<DataTypeA>());
            //    config.For<IFileReaderService<DataTypeB>>().Add(new CSVFileReaderService<DataTypeB>());
            //    config.For<IFileReaderService<DataTypeA>>().Add(new JSONFileReaderService<DataTypeA>());
            //    config.For<IFileReaderService<DataTypeB>>().Add(new JSONFileReaderService<DataTypeB>());

            //    config.Populate(serviceCollection);
            //});

            //return container.GetInstance<IServiceProvider>();

            return serviceCollection.BuildServiceProvider();
        }

        public static IContainer GetContainerProvider()
        {
            var container = new Container();
            container.Configure(config =>
            {
                // Register stuff in container, using the StructureMap APIs...
                config.For<IFileReaderService<DataTypeA>>().Add(new CSVFileReaderService<DataTypeA>()).Named("CSV");
                config.For<IFileReaderService<DataTypeB>>().Add(new CSVFileReaderService<DataTypeB>()).Named("CSV");
                config.For<IFileReaderService<DataTypeA>>().Add(new JSONFileReaderService<DataTypeA>()).Named("JSON");
                config.For<IFileReaderService<DataTypeB>>().Add(new JSONFileReaderService<DataTypeB>()).Named("JSON");
            });

            return container;
        }
    }
}
