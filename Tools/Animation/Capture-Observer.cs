if (UnityEngine.Application.isEditor) throw new System.Exception("Run this capture in the development Player.");
var camera = UnityEngine.GameObject.Find("AnimationReviewCamera").GetComponent<UnityEngine.Camera>();
var target = new UnityEngine.RenderTexture(960, 540, 24);
var previousTarget = camera.targetTexture;
var previousActive = UnityEngine.RenderTexture.active;
UnityEngine.Texture2D image = null;
try
{
    camera.targetTexture = target;
    await System.Threading.Tasks.Task.Delay(150);
    UnityEngine.RenderTexture.active = target;
    image = new UnityEngine.Texture2D(960, 540, UnityEngine.TextureFormat.RGB24, false);
    image.ReadPixels(new UnityEngine.Rect(0, 0, 960, 540), 0, 0);
    image.Apply();
    var path = System.IO.Path.Combine(System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName, "observer.png");
    System.IO.File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(image));
    return path;
}
finally
{
    camera.targetTexture = previousTarget;
    UnityEngine.RenderTexture.active = previousActive;
    if (image) UnityEngine.Object.Destroy(image);
    target.Release();
    UnityEngine.Object.Destroy(target);
}
