
using System.Collections.Generic;

namespace PPT_Item
{
    public static class PPT_EntityMockData
    {
        private const int SampleRate = 44100;
        private const int DurationSeconds = 3;
        private const int AudioDataLength = SampleRate * DurationSeconds; // 132300

        // 生成指定长度的模拟音频数据
        private static byte[] GenerateMockAudioData(byte value)
        {
            var data = new byte[AudioDataLength];
            for (int i = 0; i < AudioDataLength; i++)
            {
                data[i] = value;
            }
            return data;
        }

        public static List<PPT_Entity> GetMockPresentationList()
        {
            return new List<PPT_Entity>
            {
                new PPT_Entity(
                    "1",
                    0,
                    "欢迎来到第一张幻灯片！",
                    "https://wallspic.com/cn/image/171792-xing_zhi-da_hai-lu_xing-qi_fen-azure/3840x2160",
                    GenerateMockAudioData(1),
                    ScreenCameraType.MainCamera
                ),
                new PPT_Entity(
                    "2",
                    1,
                    "这是第二张幻灯片内容。",
                    "https://wallspic.com/cn/image/134840-de_ping_xian-huang_hun-ri_luo-yang_guang-ji_yun/3840x2160",
                    GenerateMockAudioData(2),
                    ScreenCameraType.FarCamera
                ),
                new PPT_Entity(
                    "3",
                    2,
                    "第三张幻灯片，带有跟随摄像头。",
                    "https://wallspic.com/cn/image/127033-cheng_shi-wan_shang-hai_an-wu_ye-tian_kong/3840x2160",
                    GenerateMockAudioData(3),
                    ScreenCameraType.FollowCamera
                )
            };
        }
    }
}