namespace GenericSampleCode.DataParse.Resolvers
{
    using GenericSampleCode.DataParse.FileReaders;
    public interface IFactory<T>
    {
        IFileReaderService<T> GetFileReaderInstance(string fileType);
    }
}
