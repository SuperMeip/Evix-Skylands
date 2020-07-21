using Evix.EditorTools;
using Evix.Terrain.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Evix.Sky.Stars {
  public class StarMap {

    /// <summary>
    /// How large an AU is in game voxel units
    /// </summary>
    public const int AstrologicalUnit = Chunk.Diameter * 500;

    /// <summary>
    /// An astrological object. All values are in AU
    /// </summary>
    [Serializable]
    public class AstrologicalObject {

      /// <summary>
      /// The name of the object
      /// </summary>
      public string name = "space rock";

      /// <summary>
      /// The distance from the parent, in AU
      /// </summary>
      public float distanceFromParent = 3;

      /// <summary>
      /// The scale of the object
      /// </summary>
      public float scale = 1;

      /// <summary>
      /// If the object rotates
      /// </summary>
      public bool isOrbiting = true;

      /// <summary>
      /// The initial poisition along this objects orbit in degrees it is
      /// </summary>
      [ConditionalField("isOrbiting")]
      public float initialOrbitDegree = 0;

      /// <summary>
      /// The axis that this object orbits around it's parent
      /// </summary>
      [ConditionalField("isOrbiting")]
      public Vector3 orbitalAxis = Vector3.left;

      /// <summary>
      /// How long it takes to orbit, in Game Hours
      /// </summary>
      [ConditionalField("isOrbiting")]
      public float orbitalPeriod = 20;

      /// <summary>
      /// If the object rotates
      /// </summary>
      public bool isRotating = false;

      /// <summary>
      /// The axis on which the object rotates
      /// </summary>
      [ConditionalField("isRotating")]
      public Vector3 rotationalAxis = Vector3.up;

      /// <summary>
      /// How long it takes to orbit, in Game Hours
      /// </summary>
      [ConditionalField("isRotating")]
      public float rotationalPeriod = 10;

      /// <summary>
      /// If this is the root object of the system
      /// </summary>
      public bool isRoot {
        get => parent == null;
      }

      /// <summary>
      /// The parent Astrological object of this one
      /// </summary>
      public AstrologicalObject parent;

      /// <summary>
      /// All the orbiting satelites of this object
      /// </summary>
      public List<AstrologicalObject> satelites;
    }

    AstrologicalObject[] suns;

    AstrologicalObject[] moons;

    AstrologicalObject[] wanderingStars;

    AstrologicalObject Sun = new AstrologicalObject() { distanceFromParent = 1.0f };
  }
}
