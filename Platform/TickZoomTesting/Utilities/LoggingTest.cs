using NUnit.Framework;
using TickZoom.Api;

namespace TickZoom.Utilities
{
    [TestFixture]
    public class LoggingTest
    {
        private static Log log = Factory.UserLog.GetLogger("TickZoom.Utilities.LoggingTest");

        [Test]
        public void UnlimitedArgsTest()
        {
            log.NoticeFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                             0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.InfoFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                           0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.WarnFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                           0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.ErrorFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                            0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.FatalFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                            0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.DebugFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                            0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.TraceFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                            0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
            log.VerboseFormat("{0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}",
                              0, 1, 2, 3, 4, 5, 6, 7, 8, 7, 10);
        }
    }
}