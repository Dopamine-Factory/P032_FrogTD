#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

extern "C" {
    void _IOS_PlayImpactHaptic(int style) {
        UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc]
            initWithStyle:(UIImpactFeedbackStyle)style];  // 0:Light, 1:Medium, 2:Heavy
        [generator prepare];
        [generator impactOccurred];
    }
    
    void _IOS_PlayNotificationHaptic(int type) {
        UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
        [generator prepare];
        UINotificationFeedbackType fbType = (type == 0) ? UINotificationFeedbackTypeSuccess :
                                           (type == 1) ? UINotificationFeedbackTypeWarning :
                                                         UINotificationFeedbackTypeError;
        [generator notificationOccurred:fbType];
    }
}
