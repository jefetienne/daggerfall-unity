using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;

namespace DaggerfallWorkshop
{
	public class DaggerfallStaticChairs : MonoBehaviour, IPlayerActivable
	{
		
		private static readonly HashSet<uint> flipRotation = new HashSet<uint> { 41126 };

		public uint ModelIdNum { get; set; }

		// Use this for initialization
		void Start ()
		{
			
		}
		
		// Update is called once per frame
		void Update ()
		{
			
		}
		
		public void Activate(RaycastHit hit)
		{

			var mouseLook = GameManager.Instance.PlayerGPS.GetComponentInChildren<PlayerMouseLook>();
			var heightChanger = GameManager.Instance.PlayerMotor.GetComponent<PlayerHeightChanger>();

			float flip = flipRotation.Contains(ModelIdNum) ? -1 : 1;
			mouseLook.SetFacing(transform.rotation.eulerAngles.y * flip, 0);

			GameManager.Instance.PlayerGPS.transform.position = transform.position;
			
			heightChanger.HeightAction = HeightChangeAction.DoCrouching;
			
		}
	}
}
