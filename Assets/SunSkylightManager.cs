using Evix.Controllers;
using Evix.Terrain.DataGeneration.Stars;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Evix.Managers {

  public class SunSkylightManager : MonoBehaviour {

    /// <summary>
    /// The suns we want to use to update the sky color
    /// </summary>
    public AstrologicalObjectController[] suns;

    /// <summary>
    /// The skybox material to use
    /// </summary>
    public Material skybox;

    /// <summary>
    /// The brightest sky color a single sun can make it.
    /// </summary>
    public Color brightestSkyPerSun;

    /// <summary>
    /// How often to check the sky brightness in in game hours
    /// </summary>
    public float timeCheckFrequencey = 1.0f;

    /// <summary>
    /// The starting color
    /// </summary>
    Color baseColor;

    /// <summary>
    /// Timer for the timeCheckFrequencey
    /// </summary>
    float timeCheckTimer = 0;

    // Start is called before the first frame update
    void Start() {
      baseColor = skybox.GetColor("_Tint");
    }

    // Update is called once per frame
    void Update() {
      timeCheckTimer += Time.deltaTime;
      if (timeCheckTimer >= timeCheckFrequencey * World.HourInSeconds) {
        timeCheckTimer = 0;
        Color skyColor = baseColor;
        foreach (AstrologicalObjectController sun in suns) {
          // get how high the sun is in the sky vs it's max.
          float heightPercentage = sun.transform.position.y // current heitgh
            / (sun.astrologicalObjectData.distanceFromParent * StarMap.AstrologicalUnit); // divided by the max height of this sun possible
          // add the colors together for each sun
          skyColor += Color.Lerp(baseColor, brightestSkyPerSun, heightPercentage);
        }

        // lerp them
        skyColor /= suns.Length;

        // set the skuybox color to the newo ne
        skybox.SetColor("_Tint", skyColor);
      }
    }

    /// <summary>
    /// Set the material we messed with back to normal
    /// </summary>
    void OnDestroy() {
      skybox.SetColor("_Tint", baseColor);  
    }
  }
}
