'use strict';

angular.module('app')
  .factory('ControllerFactory', ['$state', 'backendService', 'toaster', 'gettext', 'breeze', 'dialogs', '$localStorage', '$q',
      function ($state, backendService, toaster, gettext, breeze, dialogs, $localStorage, $q) {
          var list = function ($scope, settings) {
              $scope.genericSettings = settings;

              var retrievingList = false;
              var storageSetting = $state.current.name + 'GridSettings';
              var _backendService = settings.backendService || backendService;

              if (!settings.columns) {
                  settings.columnDefinitions = [
                      {
                          field: "Name",
                          displayName: gettext("Name"),
                          cellTemplate: '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="' +
                          settings.editState + '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>'
                      }
                  ];
              } else {
                  settings.columnDefinitions = [];
                  angular.forEach(settings.columns, function (v, k) {
                      var field = v[0];
                      var name = v[1];
                      var cellTemplate = '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text><a href ui-sref="' +
                          settings.editState + '({ id: row.getProperty(\'Id\') })">{{COL_FIELD}}</a></span></div>';
                      if (v.length > 2 && v[2]) {
                          cellTemplate = '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>' + v[2] + '</span></div>';
                      }

                      var sortable = true;
                      if (v.length > 3) {
                          sortable = sortable && v[3];
                      }

                      settings.columnDefinitions.push(
                      {
                          field: field,
                          displayName: name,
                          cellTemplate: cellTemplate,
                          sortable: sortable
                      });
                  });
              }

              $scope.loadGridData = function () {
                  retrievingList = true;
                  setTimeout(function () {
                      var data;

                      var fields = [];
                      for (var i = 0; i < $scope.gridOptions.sortInfo.fields.length; i++) {
                          var ordering = $scope.gridOptions.sortInfo.fields[i] + ($scope.gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                          fields.push(ordering);
                      }

                      var order = fields.join(',');

                      var entityQuery = new breeze.EntityQuery(settings.collectionType);

                      if (settings.expand) {
                          entityQuery = entityQuery.expand(settings.expand);
                      }

                      if (order) {
                          entityQuery = entityQuery
                              .orderBy(order);
                      }

                      entityQuery = entityQuery
                          .skip($localStorage[storageSetting].pageSize * ($localStorage[storageSetting].currentPage - 1))
                          .take($localStorage[storageSetting].pageSize)
                          .inlineCount(true)
                          .using(_backendService);

                      if ($scope.filter) {
                          entityQuery = entityQuery.where($scope.filter);
                      }

                      entityQuery
                          .execute()
                          .then(function (res) {
                              $scope.totalServerItems = res.inlineCount;
                              var map = settings.resultMap || function (r) {
                                  return r;
                              };
                              $scope.list = res.results.map(map);

                              retrievingList = false;
                              if (!$scope.$$phase) {
                                  $scope.$apply();
                              }
                          })
                      .catch(function () { console.log(arguments); });
                  }, 100);
              };

              var watchFunction = function () {
                  if (retrievingList == false) {

                      $localStorage[storageSetting].pageSize = parseInt($scope.pagingOptions.pageSize);
                      $localStorage[storageSetting].currentPage = parseInt($scope.pagingOptions.currentPage);
                      $localStorage[storageSetting].sortInfo = $scope.gridOptions.sortInfo;
                      $scope.loadGridData();
                  }
              };

              if (!angular.isDefined($localStorage[storageSetting])) {
                  $localStorage[storageSetting] = {
                      pageSize: 250,
                      currentPage: 1,
                      sortInfo: {
                          fields: ['Id'],
                          directions: ['asc']
                      }
                  };
              }

              $scope.totalServerItems = 0;
              $scope.pagingOptions = {
                  pageSizes: [250, 500, 1000],
                  pageSize: $localStorage[storageSetting].pageSize,
                  currentPage: $localStorage[storageSetting].currentPage
              };

              $scope.gridOptions = {
                  data: 'list',
                  enablePaging: true,
                  showFooter: true,
                  rowHeight: 36,
                  headerRowHeight: 36,
                  totalServerItems: 'totalServerItems',
                  sortInfo: $localStorage[storageSetting].sortInfo,
                  pagingOptions: $scope.pagingOptions,
                  enableRowSelection: false,
                  enableHighlighting: true,
                  useExternalSorting: true,
                  columnDefs: settings.columnDefinitions
              };

              $scope.$watch('pagingOptions', watchFunction, true);
              $scope.$watch('gridOptions.sortInfo', watchFunction, true);

          };
          var create = function ($scope, settings) {
              $scope.genericSettings = settings;

              var _backendService = settings.backendService || backendService;
              $scope.isEditing = true;
              $scope.isNew = true;
              $scope.entity = _backendService.createEntity(settings.entityType, settings.defaults);

              $scope.save = function () {
                  _backendService.saveChanges([$scope.entity]).then(function (ne) {
                      toaster.pop('success', gettext('Success'), gettext('Record successfully saved.'));
                      $state.go(settings.editState, { id: ne.entities[0].Id });
                  }).catch(function (error) {
                      toaster.pop('error', gettext('Error'), error);
                      console.log(arguments);
                  });
              };
          };
          var edit = function ($scope, settings) {
              $scope.genericSettings = settings;

              var _backendService = settings.backendService || backendService;
              $scope.isEditing = false;

              if (!settings.postSave) {
                  settings.postSave = function () {
                      var d = $q.defer();
                      d.resolve.apply(d, arguments);
                      return d.promise;
                  };
              }


              $scope.save = function (andContinue) {
                  $scope.isEditing = false;

                  _backendService.saveChanges([$scope.entity])
                      .then(settings.postSave).then(function (ne) {
                          toaster.pop('success', gettext('Success'), gettext('Record successfully saved.'));

                          if (!andContinue)
                              $state.go(settings.listState);
                      }).catch(function (error) {
                          console.log(arguments);

                          toaster.pop('error', gettext('Error'), error);
                      });
              };

              $scope.delete = function () {
                  var dlg = dialogs.confirm(gettext("Confirm"), gettext("Are you sure you would like to delete this record? This operation cannot be reversed."));
                  dlg.result.then(function () {
                      $scope.entity.entityAspect.setDeleted();
                      $scope.isEditing = false;

                      _backendService.saveChanges([$scope.entity]).then(function () {
                          toaster.pop('success', gettext('Success'), gettext('Record successfully deleted.'));
                          $state.go(settings.listState);
                      }).catch(function (error) {
                          console.log(arguments);

                          toaster.pop('error', gettext('Error'), error);
                      });
                  });
              };

              $scope.startEditing = function () {
                  $scope.isEditing = true;
              };
              $scope.endEditing = function () {
                  $scope.isEditing = false;
              };

              $scope.loadData = function () {
                  var defer = $q.defer();
                  var query = new breeze.EntityQuery(settings.collectionType)
                      .using(_backendService);

                  if (settings.expand) {
                      query = query.expand(settings.expand);
                  }

                  query = query.where("Id", "==", $state.params.id)
                      .take(1);
                  if (settings.entityType) {
                      var entityType = _backendService.metadataStore.getEntityType(settings.entityType);
                      query = query.toType(entityType);
                  }

                  query.execute()
                      .then(function (res) {
                          if (res.results) {
                              var entity = res.results.pop();

                              $scope.entity = entity;
                              defer.resolve(entity);
                          }
                      }).catch(function () {
                          console.log(arguments);
                      });

                  return defer.promise;
              };
          };
          var childGrid = function ($scope, settings) {
              var gridName = settings.name || settings.collectionType;

              var storageSetting = $state.current.name + gridName + 'GridSettings';
              settings.columnDefinitions = [];
              var _backendService = settings.backendService || backendService;

              if (!settings.columns) {
                  settings.columnDefinitions = [{ field: "Name", displayName: "Name" }];
              } else {
                  angular.forEach(settings.columns, function (v, k) {
                      var field = v[0];
                      var name = v[1];
                      var cellTemplate = null;

                      if (v.length > 2 && v[2]) {
                          cellTemplate = '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>' + v[2] + '</span></div>';
                      }

                      var sortable = true;
                      if (v.length > 3) {
                          sortable = sortable && v[3];
                      }

                      settings.columnDefinitions.push(
                      {
                          field: field,
                          displayName: name,
                          cellTemplate: cellTemplate,
                          sortable: sortable
                      });
                  });
              }

              $scope[gridName] = [];
              $scope[gridName + 'PagingOptions'] = {
                  pageSizes: [50, 100, 200, 500, 1000],
                  pageSize: 50,
                  currentPage: 1
              };
              $scope[gridName + 'GridOptions'] = {
                  data: gridName,
                  enablePaging: true,
                  showFooter: true,
                  rowHeight: 36,
                  headerRowHeight: 36,
                  totalServerItems: gridName + '_Count',
                  sortInfo: {
                      fields: ['Id'],
                      directions: ['asc']
                  },
                  pagingOptions: $scope[gridName + 'PagingOptions'],
                  enableRowSelection: false,
                  useExternalSorting: true,
                  enableHighlighting: true,
                  columnDefs: settings.columnDefinitions
              };


              $scope[gridName + 'LoadGrid'] = function () {
                  if (!$scope.entity || !$scope.entity.Id) {
                      return;
                  }

                  setTimeout(function () {
                      var fields = [];
                      var gridOptions = $scope[gridName + 'GridOptions'];
                      var pagingOptions = $scope[gridName + 'PagingOptions'];

                      for (var i = 0; i < gridOptions.sortInfo.fields.length; i++) {
                          var ordering = gridOptions.sortInfo.fields[i] + (gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                          fields.push(ordering);
                      }

                      var order = fields.join(',');

                      var entityQuery = new breeze.EntityQuery(settings.collectionType);
                      if (settings.parameters) {
                          entityQuery = entityQuery.withParameters(settings.parameters);
                      }

                      if (settings.expand) entityQuery = entityQuery.expand(settings.expand);
                      if (order) entityQuery = entityQuery.orderBy(order);

                      entityQuery = entityQuery
                          .skip(parseInt(pagingOptions.pageSize * (pagingOptions.currentPage - 1)))
                          .take(parseInt(pagingOptions.pageSize))
                          .inlineCount(true)
                          .using(_backendService);

                      var keyFilter = {};
                      if (settings.key)
                          keyFilter[settings.key] = { '==': $scope.entity.Id };

                      if ($scope[gridName + 'Filter']) {
                          keyFilter = angular.extend(keyFilter, $scope[gridName + 'Filter']);
                      }

                      $scope[gridName + 'Filter'] = keyFilter;

                      if (!$.isEmptyObject(keyFilter))
                          entityQuery = entityQuery.where(keyFilter);

                      if (settings.entityType) {
                          var entityType = _backendService.metadataStore.getEntityType(settings.entityType);
                          entityQuery = entityQuery.toType(entityType);
                      }

                      entityQuery.execute()
                          .then(function (res) {
                              $scope[gridName + '_Count'] = res.inlineCount;
                              $scope[gridName] = res.results;
                          })
                        .catch(function () {
                            console.log(arguments);
                        });
                  }, 100);
              };

              var watchFunction = function () {
                  $scope[gridName + 'LoadGrid']();
              };

              $scope.$watch(gridName + 'PagingOptions', watchFunction, true);
              $scope.$watch(gridName + 'GridOptions.sortInfo', watchFunction, true);
          };

          return {
              List: list,
              Create: create,
              Edit: edit,
              ChildGrid: childGrid
          };
      }]);

angular.module('app')
  .factory('subGrid', ['$state', 'backendService', 'toaster', 'gettext',
      function ($state, backendService, toaster, gettext) {
          var subGrid = function ($scope, settings) {
              var gridName = settings.name || settings.collectionType;

              var storageSetting = $state.current.name + gridName + 'GridSettings';
              settings.columnDefinitions = [];
              var _backendService = settings.backendService || backendService;

              if (!settings.columns) {
                  settings.columnDefinitions = [{ field: "Name", displayName: "Name" }];
              } else {
                  angular.forEach(settings.columns, function (v, k) {
                      var field = v[0];
                      var name = v[1];
                      var cellTemplate = null;

                      if (v.length > 2 && v[2]) {
                          cellTemplate = '<div class="ngCellText" ng-class="col.colIndex()"><span ng-cell-text>' + v[2] + '</span></div>';
                      }

                      var sortable = true;
                      if (v.length > 3) {
                          sortable = sortable && v[3];
                      }

                      settings.columnDefinitions.push(
                      {
                          field: field,
                          displayName: name,
                          cellTemplate: cellTemplate,
                          sortable: sortable
                      });
                  });
              }

              $scope[gridName] = [];
              $scope[gridName + 'PagingOptions'] = {
                  pageSizes: [50, 100, 200, 500, 1000],
                  pageSize: 50,
                  currentPage: 1
              };
              $scope[gridName + 'GridOptions'] = {
                  data: gridName,
                  enablePaging: true,
                  showFooter: true,
                  rowHeight: 36,
                  headerRowHeight: 36,
                  totalServerItems: gridName + '_Count',
                  sortInfo: {
                      fields: ['Id'],
                      directions: ['asc']
                  },
                  pagingOptions: $scope[gridName + 'PagingOptions'],
                  enableRowSelection: false,
                  useExternalSorting: true,
                  enableHighlighting: true,
                  columnDefs: settings.columnDefinitions
              };


              $scope[gridName + 'LoadGrid'] = function () {
                  if (!$scope.entity || !$scope.entity.Id) {
                      return;
                  }

                  setTimeout(function () {
                      var fields = [];
                      var gridOptions = $scope[gridName + 'GridOptions'];
                      var pagingOptions = $scope[gridName + 'PagingOptions'];

                      for (var i = 0; i < gridOptions.sortInfo.fields.length; i++) {
                          var ordering = gridOptions.sortInfo.fields[i] + (gridOptions.sortInfo.directions[i] == "desc" ? " desc" : "");

                          fields.push(ordering);
                      }

                      var order = fields.join(',');

                      var entityQuery = new breeze.EntityQuery(settings.collectionType);
                      if (settings.parameters) {
                          entityQuery = entityQuery.withParameters(settings.parameters);
                      }

                      if (settings.expand) entityQuery = entityQuery.expand(settings.expand);
                      if (order) entityQuery = entityQuery.orderBy(order);

                      entityQuery = entityQuery
                          .skip(parseInt(pagingOptions.pageSize * (pagingOptions.currentPage - 1)))
                          .take(parseInt(pagingOptions.pageSize))
                          .inlineCount(true)
                          .using(_backendService);

                      var keyFilter = {};
                      if (settings.key)
                          keyFilter[settings.key] = { '==': $scope.entity.Id };

                      if ($scope[gridName + 'Filter']) {
                          keyFilter = angular.extend(keyFilter, $scope[gridName + 'Filter']);
                      }

                      $scope[gridName + 'Filter'] = keyFilter;

                      if (!$.isEmptyObject(keyFilter))
                          entityQuery = entityQuery.where(keyFilter);

                      if (settings.entityType) {
                          var entityType = _backendService.metadataStore.getEntityType(settings.entityType);
                          entityQuery = entityQuery.toType(entityType);
                      }

                      entityQuery.execute()
                          .then(function (res) {
                              $scope[gridName + '_Count'] = res.inlineCount;
                              $scope[gridName] = res.results;
                          })
                        .catch(function () {
                            console.log(arguments);
                        });
                  }, 100);
              };

              var watchFunction = function () {
                  $scope[gridName + 'LoadGrid']();
              };

              $scope.$watch(gridName + 'PagingOptions', watchFunction, true);
              $scope.$watch(gridName + 'GridOptions.sortInfo', watchFunction, true);
          };

          return subGrid;
      }]
  );

