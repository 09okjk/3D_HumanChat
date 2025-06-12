namespace PPT_Item
{
    public class PPT_Entity
    {
        public string Id { get; set; }
        public int Index { get; set; }
        public string Content { get; set; }
        public string ImageUrl { get; set; }
        // base64 音频文件
        public byte[] AudioData { get; set; }
        public ScreenCameraType ScreenCameraType { get; set; }
        
        public PPT_Entity(string id,int index, string content, string imageUrl,byte[] audioData, ScreenCameraType cameraType)
        {
            Id = id;
            Index = index;
            Content = content;
            ImageUrl = imageUrl;
            AudioData = audioData;
            ScreenCameraType = cameraType;
        }
    }
}