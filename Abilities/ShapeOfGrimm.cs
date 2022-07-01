﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nightmare_Spark
{
    internal class ShapeOfGrimm
    {
        private static GameObject Bat;
        public static int GrimmSlugVelocity;
        public static int GrimmSlugIndicatorRange;
        private static Vector2 tether;
        //private static float tetherx;
        //private static float tethery;
        public static bool gsActive = false;

        public static void GrimmSlugMovement()
        {
            var sc = HeroController.instance.spellControl;
            if (gsActive)
            {
                int gsvertical;
                int gshorizontal;
                var heroActions = InputHandler.Instance.inputActions;

                if (heroActions.up.IsPressed)
                {
                    gsvertical = GrimmSlugVelocity;
                }
                else
                {
                    if (heroActions.down.IsPressed)
                    {
                        gsvertical = -GrimmSlugVelocity;
                    }
                    else
                    {
                        gsvertical = 0;
                    }
                }
                if (heroActions.right.IsPressed)
                {
                    gshorizontal = GrimmSlugVelocity;
                    Bat.transform.localScale = new Vector3(1, 1, 1);

                }
                else
                {
                    if (heroActions.left.IsPressed)
                    {
                        gshorizontal = -GrimmSlugVelocity;
                        Bat.transform.localScale = new Vector3(-1, 1, 1);
                    }
                    else
                    {
                        gshorizontal = 0;
                    }
                }

                sc.GetState("Focus S").GetAction<SetParticleEmissionRate>(9).emissionRate.Value = 0f;
                sc.GetState("Focus S").GetAction<SetParticleEmissionRate>(10).emissionRate.Value = 0f;
                sc.GetState("Focus Left").GetAction<SetParticleEmissionRate>(9).emissionRate.Value = 0f;
                sc.GetState("Focus Left").GetAction<SetParticleEmissionRate>(10).emissionRate.Value = 0f;
                sc.GetState("Focus Right").GetAction<SetParticleEmissionRate>(9).emissionRate.Value = 0f;
                sc.GetState("Focus Right").GetAction<SetParticleEmissionRate>(10).emissionRate.Value = 0f;
                HeroController.instance.GetComponent<Rigidbody2D>().velocity = new Vector2(gshorizontal, gsvertical);

            }

            if (Bat != null)
            {
                Bat.transform.position = HeroController.instance.transform.position - new Vector3(0, 1, 0);
            }

            if (gsActive && !sc.GetState("Focus Cancel 2").GetAction<SetBoolValue>(16).boolVariable.Value || gsActive && !sc.GetState("Focus Get Finish 2").GetAction<SetBoolValue>(15).boolVariable.Value)
            {
                gsActive = false;

                sc.AddTransition("Focus S", "LEFT GROUND", "Grace Check 2");
                sc.AddTransition("Focus Left", "LEFT GROUND", "Grace Check 2");
                sc.AddTransition("Focus Right", "LEFT GROUND", "Grace Check 2");

                HeroController.instance.AffectedByGravity(true);

                HeroController.instance.transform.Find("Focus Effects").Find("Lines Anim").GetComponent<tk2dSprite>().color = new Color(1f, 1, 1, 1);


                Bat.SetActive(false);
                Bat.GetComponent<tk2dSpriteAnimator>().Play("Bat End");

                HeroController.instance.gameObject.GetComponent<MeshRenderer>().enabled = true;
            }

        }
        public static void GrimmSlug()
        {
            var sc = HeroController.instance.spellControl;
            HeroController.instance.AffectedByGravity(false);

            sc.RemoveTransition("Focus S", "LEFT GROUND");
            sc.RemoveTransition("Focus Left", "LEFT GROUND");
            sc.RemoveTransition("Focus Right", "LEFT GROUND");

            Bat = GameObject.Instantiate(Nightmare_Spark.realBat);
            Bat.SetActive(true);
            GameObject.Destroy(Bat.LocateMyFSM("Control"));
            Bat.RemoveComponent<HealthManager>();
            Bat.GetComponent<MeshRenderer>().enabled = true;
           



            HeroController.instance.transform.Find("Focus Effects").Find("Lines Anim").GetComponent<tk2dSprite>().color = new Color(0.7f, 0, 0, 1);



            HeroController.instance.gameObject.GetComponent<MeshRenderer>().enabled = false;

            if (PlayerData.instance.GetBool($"equippedCharm_7"))
            {
                if (PlayerData.instance.GetBool($"equippedCharm_34"))
                {
                    // Quick Focus + Deep Focus 
                    GrimmSlugVelocity = 12;
                    GrimmSlugIndicatorRange = 7;
                }
                else
                {
                    // Quick Focus
                    GrimmSlugVelocity = 18;
                    GrimmSlugIndicatorRange = 5;
                }
            }
            else
            {
                if (PlayerData.instance.GetBool("equippedCharm_34"))
                {
                    // Deep Focus
                    GrimmSlugVelocity = 9;
                    GrimmSlugIndicatorRange = 8;
                }
                else
                {
                    // Base
                    GrimmSlugVelocity = 15;
                    GrimmSlugIndicatorRange = 6;
                }
            }

            gsActive = true;

            tether = HeroController.instance.transform.position;
            GameObject circle = new();
            circle.name = "circle";
            circle.AddComponent<Circle>();
            circle.transform.position = HeroController.instance.transform.position - new Vector3(0, 1.1f, 0);
            GameManager.instance.StartCoroutine(Timer(circle));

        }
        private static IEnumerator Timer(GameObject circle)
        {
            yield return new WaitWhile(() => HeroController.instance.spellControl.FsmVariables.FindFsmBool("Focusing").Value)
            {

            };
            GameObject.Destroy(circle);


        }
        [RequireComponent(typeof(LineRenderer))]
        public class Circle : MonoBehaviour
        {
            [Range(0, 55)]
            public int segments = 70;
            [Range(0, 5)]
            public float xradius = GrimmSlugIndicatorRange;
            [Range(0, 5)]
            public float yradius = GrimmSlugIndicatorRange;
            LineRenderer limit;

            public Color startColor = Color.red;
            public Color endColor = Color.red;
            private void Start()
            {
                limit = gameObject.GetComponent<LineRenderer>();
                limit.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
                limit.name = "Limit";
                limit.startWidth = .2f;
                limit.endWidth = .2f;
                limit.SetVertexCount(segments + 1);
                limit.useWorldSpace = false;
                CreatePoints();
                float alpha = 1.0f;
                Gradient gradient = new();
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(startColor, 1.0f), new GradientColorKey(endColor, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(alpha, 1.0f), new GradientAlphaKey(alpha, 1.0f) }
                );
                limit.colorGradient = gradient;

            }
            void CreatePoints()
            {
                float x;
                float y;
                //float z;

                float angle = 20f;

                for (int i = 0; i < (segments + 1); i++)
                {
                    x = Mathf.Sin(Mathf.Deg2Rad * angle) * xradius;
                    y = Mathf.Cos(Mathf.Deg2Rad * angle) * yradius;

                    limit.SetPosition(i, new Vector3(x, y, 0));

                    angle += (360f / segments);
                }
            }
            private Vector3 previousheropos;
            public void LateUpdate()
            {
                var HCpos = HeroController.instance.transform.position;

                var diff = new Vector2(HCpos.x - tether.x, HCpos.y - tether.y);
                if (diff.magnitude > GrimmSlugIndicatorRange)
                {
                    HeroController.instance.transform.position = previousheropos;
                }
                else
                {
                    previousheropos = HCpos;
                }

            }
        }
    }
}
