using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using System.Xml.Serialization;
using TalonAdmin.Utils;

namespace TalonAdmin.Models.Admin
{
    public class CountrySettings : Entity
    {
        private readonly JObject _propertyDictionary = new JObject();

        private bool raiseDictionaryEvents = true;
        private bool raiseListChangedEvents = true;

        public CountrySettings()
        {
            this.PropertyCollection = new ObservableCollection<CountrySettingsProperty>();

            #region Event Handlers
            _propertyDictionary.CollectionChanged += (sender, e) =>
            {
                if (!raiseDictionaryEvents)
                    return;

                raiseListChangedEvents = false;

                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (var item in e.NewItems.Cast<JProperty>())
                    {
                        this.PropertyCollection.Add(new CountrySettingsProperty
                        {
                            Settings = this,
                            SettingsId = this.Id,
                            Name = item.Name,
                            Value = item.Value.ToString(),
                        });
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
                {
                    foreach (var item in e.NewItems.Cast<JProperty>())
                    {
                        var changed = this.PropertyCollection.Where(p => p.Name == item.Name).First();
                        changed.Value = item.Value.ToString();
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                {
                    foreach (var item in e.NewItems.Cast<JProperty>())
                    {
                        var removed = this.PropertyCollection.Where(p => p.Name == item.Name).First();
                        this.PropertyCollection.Remove(removed);
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                {
                    this.PropertyCollection.Clear();
                    this.PropertyCollection = new ObservableCollection<CountrySettingsProperty>(_propertyDictionary.Properties()
                        .ToDictionary(p => p.Name, p => p.Value.ToString())
                        .Select(item => new CountrySettingsProperty
                        {
                            Settings = this,
                            SettingsId = this.Id,
                            Name = item.Key,
                            Value = item.Value,
                        }).ToArray()
                    );
                }


                raiseListChangedEvents = true;
            };

            this.PropertyCollection.CollectionChanged += (sender, e) =>
            {
                if (!raiseListChangedEvents)
                    return;

                this.raiseDictionaryEvents = false;

                _propertyDictionary.RemoveAll();
                foreach (var item in this.PropertyCollection.ToDictionary(p => p.Name, p => p.Value))
                {
                    _propertyDictionary.Add(item.Key, item.Value);
                }

                this.raiseDictionaryEvents = true;
            };
            #endregion

        }

        public virtual SmsBackendType SmsBackendType { get; set; }
        public virtual string ServiceUrl { get; set; }
        public virtual string ServiceUser { get; set; }
        public virtual string ServicePassword { get; set; }

        public virtual ObservableCollection<CountrySettingsProperty> PropertyCollection
        {
            get;
            set;
        }

        [NotMapped]
        public virtual JObject Properties
        {
            get
            {
                return _propertyDictionary;
            }
        }
    }

    public class CountrySettingsProperty
    {
        [Key, Column(Order = 0)]
        public virtual int SettingsId { get; set; }
        [Key, Column(Order = 1)]
        public virtual string Name { get; set; }

        public virtual string Value { get; set; }

        public CountrySettings Settings { get; set; }
    }

    public enum SmsBackendType
    {
        RescueSMS = 1,
        Twilio = 2,
        CustomRest = 3
    }
}