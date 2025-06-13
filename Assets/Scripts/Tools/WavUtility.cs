using System;
using UnityEngine;

namespace Tools
{
    public static class WavUtility
    {
        /// <summary>
        /// 将WAV字节数组转换为AudioClip
        /// </summary>
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
        
        /// <summary>
        /// 将AudioClip转换为WAV字节数组
        /// </summary>
        public static byte[] FromAudioClip(AudioClip clip, float[] samples, int frequency, int channels)
        {
            try
            {
                int sampleCount = samples.Length;
                
                // WAV文件头
                byte[] header = new byte[44];
                
                // RIFF chunk
                header[0] = 0x52; // R
                header[1] = 0x49; // I
                header[2] = 0x46; // F
                header[3] = 0x46; // F
                
                // 文件大小 - 8
                int fileSize = sampleCount * 2 + 36;
                header[4] = (byte)(fileSize & 0xFF);
                header[5] = (byte)((fileSize >> 8) & 0xFF);
                header[6] = (byte)((fileSize >> 16) & 0xFF);
                header[7] = (byte)((fileSize >> 24) & 0xFF);
                
                // WAVE
                header[8] = 0x57;  // W
                header[9] = 0x41;  // A
                header[10] = 0x56; // V
                header[11] = 0x45; // E
                
                // fmt chunk
                header[12] = 0x66; // f
                header[13] = 0x6D; // m
                header[14] = 0x74; // t
                header[15] = 0x20; // (space)
                
                // fmt chunk size
                header[16] = 16;
                header[17] = 0;
                header[18] = 0;
                header[19] = 0;
                
                // Audio format (PCM = 1)
                header[20] = 1;
                header[21] = 0;
                
                // Channels
                header[22] = (byte)channels;
                header[23] = 0;
                
                // Sample rate
                header[24] = (byte)(frequency & 0xFF);
                header[25] = (byte)((frequency >> 8) & 0xFF);
                header[26] = (byte)((frequency >> 16) & 0xFF);
                header[27] = (byte)((frequency >> 24) & 0xFF);
                
                // Byte rate
                int byteRate = frequency * channels * 2;
                header[28] = (byte)(byteRate & 0xFF);
                header[29] = (byte)((byteRate >> 8) & 0xFF);
                header[30] = (byte)((byteRate >> 16) & 0xFF);
                header[31] = (byte)((byteRate >> 24) & 0xFF);
                
                // Block align
                int blockAlign = channels * 2;
                header[32] = (byte)blockAlign;
                header[33] = 0;
                
                // Bits per sample
                header[34] = 16;
                header[35] = 0;
                
                // data chunk
                header[36] = 0x64; // d
                header[37] = 0x61; // a
                header[38] = 0x74; // t
                header[39] = 0x61; // a
                
                // Data size
                header[40] = (byte)(sampleCount * 2 & 0xFF);
                header[41] = (byte)((sampleCount * 2 >> 8) & 0xFF);
                header[42] = (byte)((sampleCount * 2 >> 16) & 0xFF);
                header[43] = (byte)((sampleCount * 2 >> 24) & 0xFF);
                
                // 转换样本数据
                byte[] data = new byte[sampleCount * 2];
                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = (short)(samples[i] * 32767f);
                    data[i * 2] = (byte)(sample & 0xFF);
                    data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                }
                
                // 合并头部和数据
                byte[] wav = new byte[header.Length + data.Length];
                Array.Copy(header, 0, wav, 0, header.Length);
                Array.Copy(data, 0, wav, header.Length, data.Length);
                
                return wav;
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