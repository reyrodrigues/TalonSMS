var __indexOf = [].indexOf || function (item) {
    for (var i = 0, l = this.length; i < l; i++) {
        if (i in this && this[i] === item) {
            return i;
        }
    }
    return -1;
};

angular.module('talon.common')
  .directive('uiNav', ['$timeout', function ($timeout) {
      return {
          restrict: 'AC',
          link: function (scope, el, attr) {
              var _window = $(window),
              _mb = 768,
              wrap = $('.app-aside'),
              next,
              backdrop = '.dropdown-backdrop';
              // unfolded
              el.on('click', 'a', function (e) {
                  if (next) {
                      next.trigger('mouseleave.nav');
                  }
                  var _this = $(this);
                  _this.parent().siblings(".active").toggleClass('active');
                  if (_this.next().is('ul') && _this.parent().toggleClass('active')) {
                      e.preventDefault();
                  }
                  // mobile
                  if (_this.next().is('ul') || _window.width() < _mb) {
                      $('.app-aside').removeClass('show off-screen');
                  }
              });

              // folded & fixed
              el.on('mouseenter', 'a', function (e) {
                  if (next) { next.trigger('mouseleave.nav'); }
                  $('> .nav', wrap).remove();
                  if (!$('.app-aside-fixed.app-aside-folded').length || (_window.width() < _mb) || $('.app-aside-dock').length) {
                      return;
                  }
                  var _this = $(e.target), top, w_h = $(window).height(), offset = 50, min = 150;

                  if (!_this.is('a')) {
                      (_this = _this.closest('a'));
                  }
                  if (_this.next().is('ul')) {
                      next = _this.next();
                  } else {
                      return;
                  }

                  _this.parent().addClass('active');
                  top = _this.parent().position().top + offset;
                  next.css('top', top);
                  if (top + next.height() > w_h) {
                      next.css('bottom', 0);
                  }
                  if (top + min > w_h) {
                      next.css('bottom', w_h - top - offset).css('top', 'auto');
                  }
                  next.appendTo(wrap);

                  next.on('mouseleave.nav', function (e) {
                      $(backdrop).remove();
                      next.appendTo(_this.parent());
                      next.off('mouseleave.nav').css('top', 'auto').css('bottom', 'auto');
                      _this.parent().removeClass('active');
                  });

                  if ($('.smart').length) {
                      $('<div class="dropdown-backdrop"/>').insertAfter('.app-aside').on('click', function (next) {
                          if (next) {
                              next.trigger('mouseleave.nav');
                          }
                      });
                  }
              });

              wrap.on('mouseleave', function (e) {
                  if (next) { next.trigger('mouseleave.nav'); }
                  $('> .nav', wrap).remove();
              });
          }
      };
  }])

.directive('uiToggleClass', ['$timeout', '$document', function ($timeout, $document) {
    return {
        restrict: 'AC',
        link: function (scope, el, attr) {
            el.on('click', function (e) {
                e.preventDefault();
                var classes = attr.uiToggleClass.split(','),
                    targets = (attr.target && attr.target.split(',')) || new Array(el),
                    key = 0;
                angular.forEach(classes, function (_class) {
                    var target = targets[(targets.length && key)];
                    if (_class.indexOf('*') !== -1) { magic(_class, target); }
                    $(target).toggleClass(_class);
                    key++;
                });
                $(el).toggleClass('active');

                function magic(_class, target) {
                    var patt = new RegExp('\\s' +
                        _class.
                          replace(/\*/g, '[A-Za-z0-9-_]+').
                          split(' ').
                          join('\\s|\\s') +
                        '\\s', 'g');
                    var cn = ' ' + $(target)[0].className + ' ';
                    while (patt.test(cn)) {
                        cn = cn.replace(patt, ' ');
                    }
                    $(target)[0].className = $.trim(cn);
                }
            });
        }
    };
}])

.directive('toggleEditing', ['$q', function ($q) {
    return {
        restrict: 'A',
        scope: {
            isEditing: "="
        },
        replace: true,
        link: function (scope, element, attrs) {
            var toggleEditing = function () {
                var isEditing = scope.isEditing || false;

                $('input:not(.keep-disabled), select:not(.keep-disabled), textarea:not(.keep-disabled)', element).prop('readonly', !isEditing);
                if (!isEditing) {
                    $('[type=checkbox]:not(.keep-disabled)').attr('disabled', 'disabled');
                    $('select:not(.keep-disabled)', element).attr('disabled', 'disabled');
                    $('[chosen]:not(.keep-disabled)').prop('disabled', true).trigger("chosen:updated");

                } else {
                    $('[type=checkbox]:not(.keep-disabled)').removeAttr('disabled');
                    $('select:not(.keep-disabled)', element).removeAttr('disabled');
                    $('[chosen]:not(.keep-disabled)').prop('disabled', false).trigger("chosen:updated");
                }
                $('.isChosen').trigger('chosen:updated');
            };

            toggleEditing();

            scope.$watch('isEditing', function () {
                toggleEditing();
            });
        }
    };
}])

.directive('subGrid', function ($compile, $timeout) {
    return {
        restrict: 'E',
        scope: {
            name: '@',
            expand: '@',
            collection: '@',
            entity: '=',
            entityManager: '@',
            defaultSort: '@',
            filter: '=',
            waitFor: '=',
            select: '=',
            parent: '=',
            parameters: '=',
            parameter: '@',
            key: '@'
        },
        controller: function ($scope, $injector, entityManagerFactory) {
            var $q = $injector.get('$q');
            var gridName = $scope.name || $scope.collection;
            var entityManager = entityManagerFactory[$scope.entityManager || 'entityManager']();

            $scope.gridName = gridName;

            var pagingOptions = {
                pageNumber: 1,
                pageSize: 25
            };
            var sortOptions = [];

            $scope.gridOptions = {
                data: gridName + '_Data',
                enablePaging: true,
                showFooter: true,
                rowHeight: 36,
                headerRowHeight: 36,
                paginationPageSizes: [25, 50, 100, 500],
                paginationPageSize: 25,
                useExternalPagination: true,
                enableRowSelection: false,
                useExternalSorting: true,
                enableHighlighting: true,
                columnDefs: [],
                onRegisterApi: function (gridApi) {
                    $scope.gridApi = gridApi;
                    $scope.gridApi.custom = {
                        reloadData: load
                    };
                    $scope.gridApi.core.on.sortChanged($scope, function (grid, options) {
                        sortOptions = options;

                        load();
                    });
                    $scope.gridApi.pagination.on.paginationChanged($scope, function (newPage, pageSize) {
                        pagingOptions.pageNumber = newPage;
                        pagingOptions.pageSize = pageSize;

                        load();
                    });

                    if ($scope.parent) {
                        $scope.parent['reload' + gridName] = load;
                    }
                }
            };


            this.addColumn = addColumn;
            this.load = load;
            var waitForDependencies = function () {
                if ($scope.waitFor && $scope.waitFor.then) {
                    return $scope.waitFor;
                } else {
                    var def = $q.defer();
                    def.resolve();
                    return def.promise;
                }
            };

            var selectMerge = function (results) {
                if ($scope.select) {
                    return $scope.select(results);
                } else {
                    var def = $q.defer();
                    def.resolve(results);
                    return def.promise;
                }
            };

            var isLoading = false;


            function addColumn(name, field, label, cellTemplate, width, disableSorting) {
                var col = {
                    name: name,
                    field: field,
                    displayName: label || field || text,
                    cellTemplate: (cellTemplate !== '') ? cellTemplate : undefined
                };

                if (width) {
                    col.width = width;
                }

                if(disableSorting) {
                    col.enableSorting = false;
                }

                $scope.gridOptions.columnDefs.push(col);
            }

            function load() {
                if (isLoading) {
                    return;
                }
                isLoading = true;

                if (!$scope.entity || !$scope.entity.id) {
                    isLoading = false;

                    return;
                }

                waitForDependencies().then(function () {

                    var fields = [];
                    var gridOptions = $scope.gridOptions;

                    if (sortOptions) {
                        fields = sortOptions
                            .sort(function (a, b) {
                                return a.sort.direction > b.sort.direction;
                            })
                            .map(function (d) {
                                return d.name + ' ' + d.sort.direction;
                            });
                    }
                    var order = fields.join(',');

                    var entityQuery = entityManagerFactory.entityQuery($scope.collection);

                    if ($scope.parameters) {
                        entityQuery = entityQuery.withParameters($scope.parameters);
                    }
                    if ($scope.parameter) {
                        var parameters = {};
                        parameters[$scope.parameter] = $scope.entity.id;
                        entityQuery = entityQuery.withParameters(parameters);
                    }

                    if ($scope.expand) {
                        entityQuery = entityQuery.expand($scope.expand.split(','));
                    }

                    if (order) {
                        entityQuery = entityQuery.orderBy(order.split(','));
                    } else {
                        if ($scope.defaultSort) {
                            entityQuery = entityQuery.orderBy([$scope.defaultSort]);
                        } else {
                            var firstField = $scope.gridOptions.columnDefs.map(function (c) { return c.field; }).filter(function (c) { return c; })[0];
                            entityQuery = entityQuery.orderBy([firstField]);
                        }
                    }

                    entityQuery = entityQuery
                        .skip(parseInt(pagingOptions.pageSize * (pagingOptions.pageNumber - 1), 10))
                        .take(parseInt(pagingOptions.pageSize, 10))
                        .inlineCount(true)
                        .using(entityManager);

                    var keyFilter = {};
                    if ($scope.key) {
                        keyFilter[$scope.key] = { '==': $scope.entity.id };
                    }

                    if ($scope.filter) {
                        keyFilter = angular.extend(keyFilter, $scope.filter);
                    }

                    if (!window.jQuery.isEmptyObject(keyFilter)) {
                        entityQuery = entityQuery.where(keyFilter);
                    }

                    if ($scope.entityType) {
                        var entityType = entityManager.metadataStore.getEntityType($scope.entityType);
                        entityQuery = entityQuery.toType(entityType);
                    }
                    entityQuery.execute()
                        .then(function (res) {
                            $scope.gridOptions.totalItems = res.inlineCount;
                            selectMerge(res.results).then(function (data) {
                                $scope[gridName + '_Data'] = data;
                            });
                            isLoading = false;
                        })
                      .catch(function () {
                          console.log(arguments);
                          isLoading = false;
                      });
                });
            }
        },
        replace: true,
        link: function ($scope, el, attr, ctrl) {
            var compiled = $compile('<div ui-grid="gridOptions" style="height:350px"  ui-grid-pagination class="grid"></div>')($scope);
            el.append(compiled);

            $scope.$watch('gridOptions', function () {
                ctrl.load();
            });
            $scope.$watch('filter', function () {
                ctrl.load();
            });
            $scope.$watch('entity', function () {
                ctrl.load();
            });

            // Hack to allow the grid to work under tabs.
            (function hack() {
                var parents = [];

                var parent = $scope;
                do {
                    parents.push(parent);
                } while (parent = parent.$parent);

                var withTabs = parents.filter(function (s) {
                    return s.tabs || false;
                });

                if (withTabs.length) {
                    var tabsScope = withTabs[0];
                    tabsScope.tabs.forEach(function (t) {
                        t.$watch('active', function () {
                            $timeout(function () {
                                window.jQuery(window).resize();
                            }, 100);
                        });
                    });
                }
            })();
        }
    };
})
.directive('subGridColumn', function ($compile) {
    return {
        restrict: 'E',
        require: '^subGrid',
        scope: {
            label: '@',
            name: '@',
            field: '@',
            width: '@',
            disableSorting: '=',
            hidden: '=',
            ngIf: '='
        },
        compile: function (el) {
            var html = el.html();
            el.html('');
            if (html.trim() === "") {
                html = null;
            }

            return {
                pre: function (scope, el1, attr, ctrl) {
                    if ((scope.ngIf === undefined) || scope.ngIf === true) {
                        ctrl.addColumn(scope.name, scope.field, scope.label, html, scope.width, scope.disableSorting);
                    }
                    $compile(angular.element('<!-- -->'))(scope);
                }
            };
        }
    };
})

.directive('chosen', function ($timeout) {
    var CHOSEN_OPTION_WHITELIST, NG_OPTIONS_REGEXP, isEmpty, snakeCase;
    NG_OPTIONS_REGEXP = /^\s*(.*?)(?:\s+as\s+(.*?))?(?:\s+group\s+by\s+(.*))?\s+for\s+(?:([\$\w][\$\w]*)|(?:\(\s*([\$\w][\$\w]*)\s*,\s*([\$\w][\$\w]*)\s*\)))\s+in\s+(.*?)(?:\s+track\s+by\s+(.*?))?$/;
    CHOSEN_OPTION_WHITELIST = [
                'noResultsText',
                'allowSingleDeselect',
                'disableSearchThreshold',
                'disableSearch',
                'enableSplitWordSearch',
                'fulltextSearch',
                'inheritSelectClasses',
                'maxSelectedOptions',
                'placeholderTextMultiple',
                'placeholderTextSingle',
                'searchContains',
                'singleBackstrokeDelete',
                'displayDisabledOptions',
                'displaySelectedOptions',
                'width'
    ];
    snakeCase = function (input) {
        return input.replace(/[A-Z]/g, function ($1) {
            return '_' + $1.toLowerCase();
        });
    };
    isEmpty = function (value) {
        var key;
        if (angular.isArray(value)) {
            return value.length === 0;
        } else if (angular.isObject(value)) {
            for (key in value) {
                if (value.hasOwnProperty(key)) {
                    return false;
                }
            }
        }
        return true;
    };
    return {
        restrict: 'A',
        require: '?ngModel',
        terminal: true,
        link: function (scope, element, attr, ngModel) {
            var chosen, defaultText, disableWithMessage, empty, initOrUpdate, match, options, origRender, removeEmptyMessage, startLoading, stopLoading, valuesExpr, viewWatch;
            element.addClass('localytics-chosen');
            options = scope.$eval(attr.chosen) || {};
            angular.forEach(attr, function (value, key) {
                if (__indexOf.call(CHOSEN_OPTION_WHITELIST, key) >= 0) {
                    return options[snakeCase(key)] = scope.$eval(value);
                }
            });
            startLoading = function () {
            };
            stopLoading = function () {
                $timeout(function () {
                    var $element = $(element);
                    $element.trigger('chosen:updated');
                });
            };
            chosen = null;
            defaultText = null;
            empty = false;
            initOrUpdate = function () {
                if (chosen) {
                    return element.trigger('chosen:updated');
                } else {
                    chosen = element.chosen(options).data('chosen');
                    return defaultText = chosen.default_text;
                }
            };
            removeEmptyMessage = function () {
                empty = false;
                return element.attr('data-placeholder', defaultText);
            };
            disableWithMessage = function () {
                empty = true;
                return element.attr('data-placeholder', chosen.results_none_found).attr('disabled', true).trigger('chosen:updated');
            };
            if (ngModel) {
                origRender = ngModel.$render;
                ngModel.$render = function () {
                    origRender();
                    return initOrUpdate();
                };
                if (attr.multiple) {
                    viewWatch = function () {
                        return ngModel.$viewValue;
                    };
                    scope.$watch(viewWatch, ngModel.$render, true);
                }
            } else {
                initOrUpdate();
            }
            attr.$observe('disabled', function () {
                return element.trigger('chosen:updated');
            });
            if (attr.ngOptions && ngModel) {
                match = attr.ngOptions.match(NG_OPTIONS_REGEXP);
                valuesExpr = match[7];
                return scope.$watchCollection(valuesExpr, function (newVal, oldVal) {
                    if (angular.isUndefined(newVal)) {
                        return startLoading();
                    } else {
                        if (empty) {
                            removeEmptyMessage();
                        }
                        stopLoading();
                        if (isEmpty(newVal)) {
                            return disableWithMessage();
                        }
                    }
                });
            }
        }
    };
})

.directive('fileDownload', ['$timeout', function ($timeout) {
    return {
        restrict: 'A',
        require: '?ngModel',
        link: function (scope, element, attrs, ngModel) {
            var a = $(element);
            if (ngModel) {
                viewWatch = function () {
                    return ngModel.$viewValue;
                };
                scope.$watch(viewWatch, function () {
                    if (ngModel.$viewValue) {
                        a.attr('href', 'data:' + (attrs['mimeType'] || 'text/plain') + ';base64,' + ngModel.$viewValue);
                    }
                }, true);
            }
        }
    };
}])

.directive('phoneNumber', ['$rootScope', function ($rootScope) {
    var slice = Array.prototype.slice;
    return {
        restrict: 'A',
        require: '?ngModel',
        link: function (scope, element, attrs, ngModel) {
            if (!ngModel) {
                return;
            }

            var countryCode = 1;
            ngModel.$parsers.push(function (modelValue) {
                return '+' + countryCode + modelValue;
            });

            ngModel.$formatters.push(function (viewValue) {
                var reg = new RegExp("^\\+" + countryCode + "|" + countryCode);

                return (viewValue || "").replace(reg, "");
            });

            $rootScope.$watch(function () { return $rootScope.country; }, function () {
                if ($rootScope.country) {
                    countryCode = $rootScope.country.CountryCallingCode;
                }
            });
        } //link
    }; //return
}])
.directive('fileReader', ['$q', function ($q) {
    var slice = Array.prototype.slice;
    return {
        restrict: 'A',
        require: '?ngModel',
        link: function(scope, element, attrs, ngModel) {
            if (!ngModel) {
                return;
            }

            ngModel.$render = function() {};

            element.bind('change', function(e) {
                var element = e.target;

                $q.all(slice.call(element.files, 0).map(readFile))
                    .then(function(values) {
                        if (element.multiple) {
                            ngModel.$setViewValue(values);
                        }
                        else {
                            ngModel.$setViewValue(values.length ? values[0] : null);
                        }
                    });

                function readFile(file) {
                    var deferred = $q.defer();

                    var reader = new FileReader();
                    reader.onload = function(e) {
                        deferred.resolve(e.target.result);
                    };
                    reader.onerror = function(e) {
                        deferred.reject(e);
                    };
                    reader.readAsDataURL(file);

                    return deferred.promise;
                }

            }); //change

        } //link
    }; //return
}])
;

