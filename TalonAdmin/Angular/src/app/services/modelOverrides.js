angular.module('talon.dataAccess')
.factory('modelOverrides', ModelOverrides);

function ModelOverrides() {
    return {
        VoucherTransactionRecord: VoucherTransactionRecord,
        Beneficiary: Beneficiary,
        CountrySettings: CountrySettings
    };

    function VoucherTransactionRecord() {
        this.statusString = "";


        this.statusToString = function () {
            switch (self.status) {
                case 0:
                    return "New";
                case 2:
                    return "Used";
                case 3:
                    return "Cancelled";
                default:
                    return "Unknown";
            }
        };
    }

    function Beneficiary() {
        this.name = "";
    }

    function CountrySettings() {
        this.properties = {};
    }
}