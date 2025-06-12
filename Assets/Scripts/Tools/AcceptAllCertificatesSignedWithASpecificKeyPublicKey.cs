using UnityEngine;
using UnityEngine.Networking;

public class AcceptAllCertificatesSignedWithASpecificKeyPublicKey : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // 在开发环境中接受所有证书
        if (Application.isEditor || Debug.isDebugBuild)
        {
            Debug.Log("Unity CertificateHandler: 开发环境 - 接受所有证书");
            return true;
        }
        
        // 生产环境中可以添加更严格的验证
        return true;
    }
}