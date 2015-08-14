angular.module('talon')
.run(function ($rootScope) {
    $rootScope.helpText = {
        Beneficiary: {
            mobileNumber: 'Required for verification. Remove all leading zeroes from number.',
            pin: 'Required for QR code and NFC delivery mechanisms.'
        }
    };
})

;