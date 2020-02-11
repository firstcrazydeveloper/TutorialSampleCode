namespace GenericSampleCode.DataParse.FileReaders
{
    using System.Collections.Generic;

    public interface IFileReaderService<T>
    {
        IEnumerable<T> ReadAndPreparedFileContent(string blobSaaSUrl);
    }
}
