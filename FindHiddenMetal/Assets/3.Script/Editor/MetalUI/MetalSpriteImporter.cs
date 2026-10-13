using UnityEditor;

// Assets/4.Sprite 아래 이미지는 항상 UI 스프라이트 설정으로 들여온다.
// 디자이너가 PNG를 덮어쓰거나 새로 넣어도 설정을 따로 만질 필요가 없다.
public class MetalSpriteImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(MetalUIKit.SpriteRoot + "/")) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.maxTextureSize = 4096;
        // 전체 화면 정적 레이어는 메모리가 커서 고품질 압축, 작은 부품은 선명도를 위해 무압축
        importer.textureCompression = assetPath.StartsWith(MetalUIKit.PartsDir + "/")
            ? TextureImporterCompression.Uncompressed
            : TextureImporterCompression.CompressedHQ;
    }
}
