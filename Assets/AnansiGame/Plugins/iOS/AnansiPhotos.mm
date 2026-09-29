// iOS only: saves a PNG from disk into the Photos app and reports back to Unity (KenteGallery.cs).
#import <UIKit/UIKit.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

@interface AnansiPhotoSaver : NSObject
- (void)image:(UIImage*)image didFinishSavingWithError:(NSError*)error contextInfo:(void*)info;
@end

@implementation AnansiPhotoSaver
- (void)image:(UIImage*)image didFinishSavingWithError:(NSError*)error contextInfo:(void*)info
{
    const char* msg = error == nil ? "ok" : [[error localizedDescription] UTF8String];
    UnitySendMessage("AnansiGallery", "OnSaved", msg);
}
@end

static AnansiPhotoSaver* anansiSaver = nil;

extern "C" void _AnansiSaveToPhotos(const char* path)
{
    NSString* p = [NSString stringWithUTF8String:path];
    UIImage* img = [UIImage imageWithContentsOfFile:p];
    if (img == nil) { UnitySendMessage("AnansiGallery", "OnSaved", "image not found"); return; }
    if (anansiSaver == nil) anansiSaver = [[AnansiPhotoSaver alloc] init];
    UIImageWriteToSavedPhotosAlbum(img, anansiSaver, @selector(image:didFinishSavingWithError:contextInfo:), NULL);
}
