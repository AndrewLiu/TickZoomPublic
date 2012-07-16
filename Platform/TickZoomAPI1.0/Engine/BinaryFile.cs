using System;

namespace TickZoom.Api
{
    public interface BinaryFile : IDisposable
    {
        void Initialize(string folderOrfile, string symbolFile, BinaryFileMode mode);
        void Initialize(string fileName, BinaryFileMode mode);
        bool TryWrite(Serializable serializable, long utcTime);
        void Write(Serializable tickIO, long utcTime);
        void GetLast(Serializable lastTickIO);
        bool TryRead(Serializable serializable);
        void Flush();
        long Length { get; }
        long Position { get; }
        int DataVersion { get; }
        int BlockVersion { get; }
        bool QuietMode { get; set; }
        string FileName { get; }
        string Name { get; }
        bool EraseFileToStart { get; set; }
        long WriteCounter { get; }
        long MaxCount { get; set; }
        TimeStamp StartTime { get; set; }
        TimeStamp EndTime { get; set; }
        long StartCount { get; set; }
        Action<Progress> ReportProgressCallback { get; set; }
        bool IsInitialized { get; }
    }
}