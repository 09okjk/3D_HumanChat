using System;
using UnityEngine;

namespace Tools
{
    public static class WavUtility
    {
        public static AudioClip ToAudioClip(byte[] wavData)
        {
            try
            {
                if (wavData.Length < 44)
                {
                    Debug.LogError("WAV数据太短，无法解析");
                    return null;
                }
                
                // 解析WAV头
                int channels = BitConverter.ToInt16(wavData, 22);
                int sampleRate = BitConverter.ToInt32(wavData, 24);
                int bitsPerSample = BitConverter.ToInt16(wavData, 34);
                
                // 找到数据块
                int dataOffset = FindDataChunk(wavData);
                if (dataOffset == -1)
                {
                    Debug.LogError("未找到WAV数据块");
                    return null;
                }
                
                int dataSize = BitConverter.ToInt32(wavData, dataOffset + 4);
                int audioDataOffset = dataOffset + 8;
                
                // 转换音频数据
                float[] samples;
                if (bitsPerSample == 16)
                {
                    samples = Convert16BitToFloat(wavData, audioDataOffset, dataSize);
                }
                else if (bitsPerSample == 8)
                {
                    samples = Convert8BitToFloat(wavData, audioDataOffset, dataSize);
                }
                else
                {
                    Debug.LogError($"不支持的位深度: {bitsPerSample}");
                    return null;
                }
                
                AudioClip audioClip = AudioClip.Create("WAVAudio", samples.Length / channels, channels, sampleRate, false);
                audioClip.SetData(samples, 0);
                
                return audioClip;
            }
            catch (Exception e)
            {
                Debug.LogError($"WAV转换失败: {e.Message}");
                return null;
            }
        }
        
        private static int FindDataChunk(byte[] wavData)
        {
            for (int i = 12; i < wavData.Length - 4; i++)
            {
                if (wavData[i] == 'd' && wavData[i + 1] == 'a' && 
                    wavData[i + 2] == 't' && wavData[i + 3] == 'a')
                {
                    return i;
                }
            }
            return -1;
        }
        
        private static float[] Convert16BitToFloat(byte[] wavData, int offset, int dataSize)
        {
            int sampleCount = dataSize / 2;
            float[] samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(wavData, offset + i * 2);
                samples[i] = sample / 32768f;
            }
            
            return samples;
        }
        
        private static float[] Convert8BitToFloat(byte[] wavData, int offset, int dataSize)
        {
            float[] samples = new float[dataSize];
            
            for (int i = 0; i < dataSize; i++)
            {
                samples[i] = (wavData[offset + i] - 128) / 128f;
            }
            
            return samples;
        }
    }
}