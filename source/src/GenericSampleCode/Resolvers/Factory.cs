namespace GenericSampleCode.DataParse.Resolvers
{
    using GenericSampleCode.DataParse.AppStart;
    using GenericSampleCode.DataParse.FileReaders;

    public class Factory<T> : IFactory<T>
    {
        public IFileReaderService<T> GetFileReaderInstance(string fileType)
        {
            var serviceContainer = Startup.GetContainerProvider();
            var instance = serviceContainer.GetInstance<IFileReaderService<T>>(fileType); ;
            return instance;

        }
    }
}
