angular.module('talon.country-admin')

.config(function config($stateProvider) {
    $stateProvider
    .state('country-admin.transaction', {
        url: '/transaction',
        template: "<div ui-view></div>",
        abstract: true,
        data: {
        }
    })

    .state('country-admin.transaction.transaction-log', {
        url: '/transaction-log',
        controller: 'TransactionLogListController as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Transaction Log',
            settings: {
                filterFunction: function (value) {
                    return {
                        'or': [
                            { 'beneficiary.firstName': { 'contains': value } },
                            { 'beneficiary.lastName': { 'contains': value } },
                            { 'vendor.name': { 'contains': value } },
                            { 'transactionCode': { 'contains': value } }
                        ]
                    };
                },
                collectionType: "TransactionLogItems",
                entityType: 'TransactionLogItem',
                expand: ['beneficiary', 'vendor'],
                defaults: {
                },
                columns: [
                    ['id', '#'],
                    ['transactionCode', 'Transaction Code'],
                    ['beneficiary.name', 'Beneficiary', true],
                    ['vendor.name', 'Vendor'],
                    ['amountCredited', 'Credit'],
                    ['amountRemaining', 'Amount Remaining on Card'],
                    ['date', 'Date'],
                    ['confirmationCode', 'Confirmation Code'],
                    ['quarantine', 'In Quarantine']
                ]
            }
        }
    })



    .state('country-admin.transaction.quarantine', {
        url: '/quarantine',
        controller: 'QuarantineListController as vm',
        templateUrl: 'index.tpl.html',
        data: {
            pageTitle: 'Transaction Quarantine',
            settings: {
                filterFunction: function (value) {
                    return {
                        'or': [
                            { 'beneficiary.firstName': { 'contains': value } },
                            { 'beneficiary.lastName': { 'contains': value } },
                            { 'vendor.name': { 'contains': value } },
                            { 'transactionCode': { 'contains': value } }
                        ]
                    };
                },
                collectionType: "TransactionLogItems",
                entityType: 'TransactionLogItem',
                expand: ['beneficiary', 'vendor'],
                defaults: {
                },
                filter: {
                    'and': [{ 'quarantine': { '==': true } }]
                },
                columns: [
                    ['id', '#'],
                    ['transactionCode', 'Transaction Code'],
                    ['beneficiary.name', 'Beneficiary', true],
                    ['vendor.name', 'Vendor'],
                    ['amountCredited', 'Credit'],
                    ['amountRemaining', 'Amount Remaining on Card'],
                    ['date', 'Date'],
                    ['confirmationCode', 'Confirmation Code']
                ]
            }
        }
    })


    ;
})
.controller('TransactionLogListController', TransactionLogListController)
.controller('QuarantineListController', QuarantineListController)

;


QuarantineListController.prototype.actionsHtml = function (data, type, full, meta) {
    return '<div class="btn-group">' +
        '<button class="btn btn-success btn-block btn-xs" ng-if="vm.canProcessTransaction" ng-click="vm.processTransaction(\'' + data.id + '\')">' +
        '   <i class="fa fa-check"></i> Process Transaction' +
        '</button></div>';
};


TransactionLogListController.prototype.actionsHtml = function (data, type, full, meta) {
    return '';
};

function TransactionLogListController($injector, $scope) {
    ListController.call(this, $injector, $scope);
}



function QuarantineListController($injector, $scope) {
    this.canProcessTransaction = $scope.canI('Process Quarantined Transactions');
    this.processTransaction = processTransaction;

    var gettext = $injector.get('gettext');
    var $http = $injector.get('$http');
    var dialogs = $injector.get('dialogs');
    var entityManagerFactory = $injector.get('entityManagerFactory');
    var entityManager = entityManagerFactory.entityManager();


    function processTransaction(id) {
        var self = this;
        var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to process this transaction? This operation cannot be reversed."));
        dlg.result.then(function (r) {
            if (r === 'yes') {
                $http.post(serviceRoot + 'api/App/MobileClient/ProcessQuarantinedTransaction?transactionId=' + id).then(function () {
                    self.success(gettext('Transaction processed successfully'));

                    self.instance.rerender();
                }).catch(function (error) {
                    self.failure(gettext(error.MessageKey));
                });
            }
        });
    }

    ListController.call(this, $injector, $scope);
}
