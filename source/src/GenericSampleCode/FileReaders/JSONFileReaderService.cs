﻿namespace GenericSampleCode.DataParse.FileReaders
{
    using System;
    using System.Collections.Generic;

    public class JSONFileReaderService<T> : IFileReaderService<T>
    {
        public IEnumerable<T> ReadAndPreparedFileContent(string blobSaaSUrl)
        {
            throw new NotImplementedException();
        }
    }
}
