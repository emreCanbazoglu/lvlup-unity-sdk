#import <Foundation/Foundation.h>

// Get build number from Info.plist (CFBundleVersion)
extern "C" {
    const char* _GetBuildNumber() {
        NSString *buildNumber = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"CFBundleVersion"];
        if (buildNumber == nil) {
            return NULL;
        }
        
        // Convert NSString to C string that Unity can use
        const char* buildNumberString = [buildNumber UTF8String];
        
        // Allocate memory and copy the string (Unity will handle deallocation)
        char* returnString = (char*)malloc(strlen(buildNumberString) + 1);
        strcpy(returnString, buildNumberString);
        
        return returnString;
    }
}

