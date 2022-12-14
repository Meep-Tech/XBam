using System;

namespace Meep.Tech.XBam {

  /// <summary>
  /// Marks a model property field as the Archetype field.
  /// Used for serialization methods
  /// </summary>
  public class ModelComponentsProperty : Attribute {
    public ModelComponentsProperty()
      : base() { }
  }
}
