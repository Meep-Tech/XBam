using Meep.Tech.Reflection;
using Meep.Tech.XBam.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Meep.Tech.XBam {

  public partial interface IModel {

    /// <summary>
    /// A modifyable parameter container that is used to build a model.
    /// The non-generic base class for utility.
    /// </summary>
    public partial interface IBuilder: XBam.IBuilder {

      /// <summary>
      /// A delegate used to modify a model.
      /// </summary>

      protected internal delegate void ModelModifier(ref IModel model, Archetype archetype, Universe universe, IBuilder? builder = null);

      /// <summary>
      /// Used to initialize the model with or without a builder.
      /// </summary>
      internal protected static void InitalizeModel(
        ref IModel? model,
        IBuilder? builder, 
        Archetype? archetype = null, 
        Universe? universe = null,
        ModelModifier? onConfigured = null,
        ModelModifier? onFinalized = null
      ) {
        universe = builder?.Universe ?? universe ?? throw new ArgumentNullException();
        archetype = builder?.Archetype ?? archetype ?? throw new ArgumentNullException();

        model ??= ((XBam.IFactory)archetype)._modelConstructor(builder 
          ?? throw new ArgumentNullException(nameof(builder)));

        if (model is null) {
          if (builder is not null) {
            model = archetype.OnModelInitialized(builder, model);

            if (model is not null) {
              if (model is IUnique unique) {
                model = unique.InitializeId(builder?.TryToGet<string>(nameof(IUnique.Id)));
              }
              onConfigured?.Invoke(ref model, archetype, universe, builder);
              model = model.OnFinalized(builder);
            }

            model = archetype.OnModelFinalized(builder, model);
            if (model is not null) {
              onFinalized?.Invoke(ref model!, archetype, universe, builder);
            }
          }
        }
        else {
          model = model.OnInitialized(archetype, universe, builder);
          if (model is IUnique unique) {
            model = unique.InitializeId(builder?.TryToGet<string>(nameof(IUnique.Id)));
          }

          if (builder is not null) {
            model = archetype.OnModelInitialized(builder, model);
          }

          onConfigured?.Invoke(ref model!, archetype, universe, builder);

          model = model!.OnFinalized(builder);

          if (builder is not null) {
            model = archetype.OnModelFinalized(builder, model);
          }

          onFinalized?.Invoke(ref model!, archetype, universe, builder);
        }
      }
    }
  }

  public partial interface IModel<TModelBase> 
    where TModelBase : IModel<TModelBase> 
  {

    /// <summary>
    /// A modifyable parameter container that is used to build a model.
    /// </summary>
    public partial struct Builder : IModel.IBuilder, IBuilder<TModelBase> {
      Dictionary<string, object> _parameters 
        => __parameters ??= new();
      Dictionary<string, object>? __parameters;

      /// <summary>
      /// The archetype/factory using this builder.
      /// </summary>
      public XBam.Archetype Archetype {
        get;
      }

      /// <summary>
      /// The universe this builder is building in
      /// </summary>
      public Universe Universe {
        get;
      }

      ///<summary><inheritdoc/></summary>
      public IEnumerable<KeyValuePair<string, object>> Parameters
        => this;


      /// <summary>
      /// Empty new builder
      /// </summary>
      public Builder(XBam.Archetype type, Universe? universe = null) {
        Archetype = type;
        Universe = universe ?? type.Universe;
        __parameters = null;
      }

      /// <summary>
      /// New builder from a collection of param names
      /// </summary>
      public Builder(XBam.Archetype type, IEnumerable<KeyValuePair<string, object>> @params, Universe universe = null) {
        Archetype = type;
        Universe = universe ?? Archetype.Universe;
        __parameters = @params is not null 
          ? new(@params) 
          : null;
      }

      #region Param Access

      /// <summary>
      /// get the param
      /// </summary>
      public object this[Param param]
        => _parameters[param.Key].CastTo(param.ValueType);

      /// <summary>
      /// get the param
      /// </summary>
      public object this[string param]
        => _parameters[param];

      ///<summary><inheritdoc/></summary>
      public IEnumerable<string> Keys
        => ((IReadOnlyDictionary<string, object>)_parameters).Keys;

      ///<summary><inheritdoc/></summary>
      public IEnumerable<object> Values
        => ((IReadOnlyDictionary<string, object>)_parameters).Values;

      ///<summary><inheritdoc/></summary>
      public int Count
        => ((IReadOnlyCollection<KeyValuePair<string, object>>)_parameters).Count;

      ///<summary><inheritdoc/></summary>
      public bool ContainsKey(string key) {
        return ((IReadOnlyDictionary<string, object>)_parameters).ContainsKey(key);
      }

      ///<summary><inheritdoc/></summary>
      public bool TryGetValue(string key, out object value) {
        return ((IReadOnlyDictionary<string, object>)_parameters).TryGetValue(key, out value);
      }

      ///<summary><inheritdoc/></summary>
      public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
        return ((IEnumerable<KeyValuePair<string, object>>)_parameters).GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
        return ((IEnumerable)_parameters).GetEnumerator();
      }

      ///<summary><inheritdoc/></summary>
      public IBuilder<TModelBase> Add(KeyValuePair<string, object> parameter)
        => (IBuilder<TModelBase>)(this as IBuilder).Add(parameter);

      ///<summary><inheritdoc/></summary>
      public IBuilder<TModelBase> Add(string key, object value) {
        _parameters.Add(key, value);
        return this;
      }

      ///<summary><inheritdoc/></summary>
      public object Get(string key)
        => _parameters[key];

      ///<summary><inheritdoc/></summary>
      public bool TryToGet(string key, out object value)
        => _parameters.TryGetValue(key, out value);

      ///<summary><inheritdoc/></summary>
      public bool Has(string key)
        => _parameters.ContainsKey(key);

      /// <summary>
      /// Check if the builder has this key
      /// </summary>
      public bool Has(Param param)
        => _parameters.ContainsKey(param.Key);

      #endregion

      /// <summary>
      /// Build the model.
      /// </summary>
      public TModelBase Make() {
        IModel producedModel = default!;
        IModel.IBuilder.InitalizeModel(ref producedModel, this, onConfigured: (ref IModel model, Archetype archetype, Universe universe, IBuilder? @this) => {
          if (model is IReadableComponentStorage componentStorage) {
            model = _initializeModelComponents(archetype, (Builder?)@this, componentStorage);
          }

#if DEBUG
          // Warns you if you've got Model Component settings in the Archetype but no Component Storage on the Model.
          else if (archetype.InitialUnlinkedModelComponents.Any() || archetype.ModelLinkedComponents.Any()) {
            Console.WriteLine($"The Archetype of Type: {archetype}, provides components to set up on the produced model of type :{model.GetType()}, but this model does not inherit from the interface {nameof(IReadableComponentStorage)}. Maybe consider adding .WithComponents to the Model<[,]> base class you are inheriting from, or removing model components added to any of the Initial[Component...] properties of the Archetype");
#warning An archetype with a Model Base Type that does not inherit from IReadableComponentStorage has been provided with InitialUnlinkedModelComponentCtors values. These components may never be applied to the desired model if it does not inhert from IReadableComponentStorage
          }
#endif

        },
        onFinalized: (ref IModel model, Archetype archetype, Universe universe, IBuilder? @this) => {
          // loging
          model.TryToLog(
            ModelLog.Entry.ActionType.Built,
            "null",
            @this,
            new Dictionary<string, object>() {
            { ModelLog.Entry.MetadataField.AutoBuilderUsed.Key, archetype.Universe.TryToGetExtraContext<Configuration.IModelAutoBuilder>()?.HasAutoBuilderSteps(archetype) ?? false }
            }
          );
        });

        return (TModelBase)producedModel;
      }

      /// <summary>
      /// Loop though each model component and initialize them.
      /// This also adds all model data componnets linked to an archetype component first.
      /// </summary>
      static TModelBase _initializeModelComponents(Archetype archetype, Builder? @this, IReadableComponentStorage storage) {
        var parentModel = storage as IModel;

        // add components built from a given ctor
        foreach ((string key, Func<IComponent.IBuilder, IModel.IComponent> ctor) in archetype.InitialUnlinkedModelComponents) {
          XBam.IComponent.IBuilder componentBuilder = _makeComponentBuilder(@this, parentModel, key, out Type componentType);

          // no provided ctor, we need to get the default one.
          if (ctor == null) {
            // build the component:
            IComponent component = (IComponent)((Archetype)Components.GetFactory(componentType))
              .Make(componentBuilder);

            storage.AddComponent(component);
          } // else use the provided ctor:
          else {
            storage.AddComponent(ctor(componentBuilder));
          }
        }

        /// add link components from the archetype
        foreach (XBam.Archetype.IComponent.ILinkedComponent linkComponent in archetype.ModelLinkedComponents) {
          XBam.IComponent.IBuilder componentBuilder = _makeComponentBuilder(@this, parentModel, linkComponent.Key, out _);
          storage.AddComponent(linkComponent.BuildDefaultModelComponent(componentBuilder, archetype.Universe));
        }

        return (TModelBase)storage;
      }

      static XBam.IComponent.IBuilder _makeComponentBuilder(Builder? parentBuilder, IModel? parentModel, string key, out Type componentType) {
        // TOOD: cache this
        componentType = Components.DefaultUniverse.Components.Get(key);
        // Make a builder to match this component with the params from the parent:
        var componentBuilder = (XBam.IComponent.IBuilder)((IBuilderSource)Components.GetFactory(componentType))
          .Build();
        componentBuilder.__parameters = parentBuilder?.__parameters;
        componentBuilder.Parent = parentModel;

        return componentBuilder;
      }
    }
  }
}