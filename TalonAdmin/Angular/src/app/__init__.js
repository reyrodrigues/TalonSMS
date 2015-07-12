var serviceRoot = window.location.protocol + '//' + window.location.host + '/';

angular.extend(EditController.prototype, {
    configure: noop,
    defaults: noop,
    preLoad: noop,
    canEdit: noopTrue
});

angular.extend(ListController.prototype, {
    configure: noop
});

function EditController($injector, $scope) {
    var entityManagerFactory = $injector.get('entityManagerFactory');
    var $state = $injector.get('$state');
    var $q = $injector.get('$q');
    var dialogs = $injector.get('dialogs');
    var toaster = $injector.get('toaster');
    var controlledLists = $injector.get('controlledLists');
    var $localStorage = $injector.get('$localStorage');
    var self = this;
    var currentSettings = ($state.current.data ? $state.current.data.settings : false) || {};
    var parentSettings = angular.extend({}, ($state.$current.parent.data ? $state.$current.parent.data.settings : false) || {});


    this.state = $state.current;
    this.$injector = $injector;
    this.$scope = $scope;
    this.isEditing = false;
    this.isNew = false;
    this.entity = {};
    this.settings = angular.extend(parentSettings, currentSettings);

    var entityManagerFunction = this.settings.entityManager || 'entityManager';
    this.entityManager = entityManagerFactory[entityManagerFunction]();


    this.backToList = backToList;
    this.save = save;
    this.remove = remove;
    this.beginEditing = beginEditing;
    this.endEditing = endEditing;
    this.defaults = defaults;
    this.lists = {};

    this.configure();
    load();

    function backToList() {
        $state.go(self.settings.listState);
    }

    function load() {
        if (self.settings.controlledLists) {
            self.settings.controlledLists.forEach(function (l) {
                controlledLists[l]().then(function(list){
                    self.lists[l] = list;
                });
            });
        }

        var defer = $q.defer();
        $q.when(self.preLoad()).then(function () {
            if ($state.params.id) {
                self.isNew = false;
                self.isEditing = false;

                var query = entityManagerFactory.entityQuery(self.settings.collectionType)
                    .using(self.entityManager);

                if (self.settings.expand) {
                    query = query.expand(self.settings.expand);
                }

                query = query.where("id", "==", $state.params.id)
                    .take(1);

                if (self.settings.entityType) {
                    var entityType = self.entityManager.metadataStore.getEntityType(self.settings.entityType);
                    query = query.toType(entityType);
                }

                query.execute()
                    .then(function (res) {
                        if (res.results) {
                            var entity = res.results.pop();

                            self.entity = entity;
                            defer.resolve(entity);
                        }
                    }).catch(function () {
                        console.log(arguments);
                    });
            } else {
                self.isNew = true;
                self.isEditing = true;

                var defaults = self.defaults();

                self.entity = self.entityManager.createEntity(self.settings.entityType, defaults);
                defer.resolve(self.entity);
            }
        });

        return defer.promise;
    }

    function beginEditing() {
        var self = this;
        self.isEditing = true;
    }

    function endEditing() {
        var self = this;
        self.isEditing = false;
    }

    function save(continueEditing) {
        var self = this;
        self.isEditing = false;

        self.entityManager.saveChanges([self.entity]).then(function (ne) {
            success('Record successfully saved.');
            if (self.isNew) {
                $state.go('^.edit', { id: self.entity.id });
            }

            self.isEditing = continueEditing;
        }).catch(function (error) {
            failure(error);
        });
    }

    function remove(entityOrId) {
        var self = this;
        var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
        dlg.result.then(function () {
            self.entity.entityAspect.setDeleted();
            self.isEditing = false;

            self.entityManager.saveChanges([self.entity]).then(function () {
                success('Record successfully deleted.');

                self.backToList();
            }).catch(function (error) {
                failure(error);
            });
        });
    }

    function success(msg) {
        toaster.success('Success!', msg);
    }

    function failure(msg) {
        toaster.error('Error!', msg);
    }

    function defaults() {
        // Application specific defaults
        
        var def = this.settings.defaults || {};

        return angular.extend(def, {
            organizationId: $localStorage.organization.Id,
            countryId: $localStorage.country.Id
        });
    }
}

function ListController($injector, $scope) {
    var entityManagerFactory = $injector.get('entityManagerFactory');
    var DTOptionsBuilder = $injector.get('DTOptionsBuilder');
    var DTColumnBuilder = $injector.get('DTColumnBuilder');
    var $compile = $injector.get('$compile');
    var $state = $injector.get('$state');
    var dialogs = $injector.get('dialogs');
    var toaster = $injector.get('toaster');
    var self = this;
    var currentSettings = ($state.current.data ? $state.current.data.settings : false) || {};
    var parentSettings = angular.extend({}, ($state.$current.parent.data ? $state.$current.parent.data.settings : false) || {});

    this.$injector = $injector;
    this.settings = angular.extend(parentSettings, currentSettings);
    this.state = $state.current;
    this.remove = remove;
    var entityManagerFunction = this.settings.entityManager || 'entityManager';
    this.entityManager = entityManagerFactory[entityManagerFunction]();

    self.instance = {};

    this.configure();
    load();

    function load() {
        self.options = DTOptionsBuilder
        .newOptions()
        .withFnServerData(function (source, data, callback, settings) {
            var oData = data.reduce(function (o, v) {
                o[v.name] = v.value;
                return o;
            }, {});

            var ordering = oData.order.map(function (o) {
                return oData.columns[o.column].data + ' ' + o.dir;
            }).join(',');

            if (!ordering) {
                ordering = 'id';
            }

            var query = entityManagerFactory.entityQuery(self.settings.collectionType);

            if (self.settings.entityType) {
                var entityType = self.entityManager.metadataStore.getEntityType(self.settings.entityType);
                query = query.toType(entityType);
            }

            if (self.settings.expand) {
                query = query.expand(self.settings.expand);
            }

            query.orderBy(ordering)
                .take(oData.length)
                .skip(oData.start)
                .using(self.entityManager)
                .inlineCount()
                .execute()
                .then(function (r) {
                    callback({
                        recordsTotal: r.inlineCount,
                        recordsFiltered: r.inlineCount,
                        data: r.results
                    });
                })
                .catch(function () { failure(arguments); });
        })
        .withDataProp('data')
        .withOption('filter', false)
        .withOption('processing', true)
        .withOption('serverSide', true)
        .withBootstrap()
        .withDisplayLength(25)
        .withOption('createdRow', createdRow)
        .withPaginationType('full_numbers');

        self.columns = self.settings.columns.map(function (c) {
            var col = DTColumnBuilder
                .newColumn(c[0])
                .withTitle(c.length > 1 ? c[1] : c[0]);

            if (c.length > 2 && c[2]) {
                col = col.notSortable();
            }

            if (c.length > 3 && c[3]) {
                col = col.notVisible();
            }

            if (c.length > 4 && c[4]) {
                col = col.renderWith(c[4]);
            }

            return col;
        });

        self.columns.push(DTColumnBuilder
            .newColumn(null)
            .withTitle('Actions')
            .withOption('width', '130px')
            .notSortable()
            .renderWith(actionsHtml));
    }

    function createdRow(row, data, dataIndex) {
        $compile(angular.element(row).contents())($scope);
    }

    function actionsHtml(data, type, full, meta) {
        return '<div class="btn-group"><button class="btn btn-success btn-xs" ui-sref="^.edit({ id: \'' + data.id + '\'})">' +
            '   <i class="fa fa-edit"></i> Edit' +
            '</button>' +
            '<button class="btn btn-danger btn-xs" ng-click="vm.remove(\'' + data.id + '\')">' +
            '   <i class="fa fa-trash-o"></i> Delete' +
            '</button></div>';
    }

    function remove(id) {
        var self = this;
        var dlg = dialogs.confirm("Confirm", "Are you sure you would like to delete this record? This operation cannot be reversed.");
        dlg.result.then(function () {
            var query = entityManagerFactory.entityQuery(self.settings.collectionType)
                .where('id', '==', id)
                .take(1)
                .using(self.entityManager)
                .execute()
                .then(function (r) {
                    var entity = r.results[0];

                    entity.entityAspect.setDeleted();
                    self.entityManager.saveChanges([entity]).then(function () {
                        success('Record successfully deleted.');

                        self.instance.rerender();
                    }).catch(function (error) {
                        failure(error);
                    });
                })
                .catch(function (r) {
                    failure(r);
                });

        });
    }

    function success(msg) {
        toaster.success('Success!', msg);
    }

    function failure(msg) {
        toaster.error('Error!', msg);
        console.log(msg);
    }
}

function noop() {
}

function noopTrue() {
    return true;
}

