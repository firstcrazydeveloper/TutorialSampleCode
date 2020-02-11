namespace GenericSampleCode
{
    using GenericSampleCode.DataParse.AppStart;
    using Microsoft.Extensions.Logging;
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using GenericSampleCode.DataParse.FileReaders;
    using GenericSampleCode.DataParse.Models;

    class Program
    {
        static void Main(string[] args)
        {
            var serviceCollection = Startup.GetServiceProvider();
            var serviceContainer = Startup.GetContainerProvider();
            ILogger<Program> logger = serviceCollection.GetService<ILoggerFactory>()
                .CreateLogger<Program>();
            Console.WriteLine("Hello World!");
            Console.WriteLine("********************Select DataType***************************");
            Console.WriteLine("Input A for DataType-A");
            Console.WriteLine("Input B for DataType-B");
            var dataType = Console.ReadLine();
            Console.WriteLine("Input CSV for CSV file");
            Console.WriteLine("Input JSON for JSON file");
            var fileType = Console.ReadLine();

            //var fileReaderServices = (IEnumerable<IFileReaderService<DataTypeA>>)serviceCollection.GetServices<IFileReaderService<DataTypeA>>();
            //var csvFileReader = fileReaderServices.First(service => service.GetType() == typeof(CSVFileReaderService<DataTypeA>));
            //var jsonFileReader = serviceContainer.GetInstance<IFileReaderService<DataTypeA>>("JSON-DataTypeA");

            switch (dataType)
            {
                case "A":
                    {
                        var fileReader = serviceContainer.GetInstance<IFileReaderService<DataTypeA>>(fileType);
                        break;
                    }
                case "B":
                    {
                        var fileReader = serviceContainer.GetInstance<IFileReaderService<DataTypeB>>(fileType);
                        break;
                    }
            }
        }
    }
}
