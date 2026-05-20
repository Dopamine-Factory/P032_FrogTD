#import <Foundation/Foundation.h>

extern "C" {
    const char* GetiOSBuildNumber() {
        NSString* buildNumber = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"];
        return strdup([buildNumber UTF8String]);
    }
}