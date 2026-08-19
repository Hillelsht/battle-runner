using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleRunner.Meta.UI
{
    /// <summary>Death prompt — the resurrect rewarded-ad touchpoint.</summary>
    public sealed class ResurrectPrompt
    {
        private readonly GameObject _root;
        private readonly Text _message;
        private readonly GameObject _resurrectGo;

        private Action _onResurrect;
        private Action _onGiveUp;

        public ResurrectPrompt(Transform canvas)
        {
            RectTransform root = UiFactory.FullscreenPanel(canvas, "Resurrect", new Color(0f, 0f, 0f, 0.82f));
            _root = root.gameObject;

            _message = UiFactory.Label(root, "Message", "YOUR FORCE HAS FALLEN", 58, UiFactory.Blood);
            UiFactory.Place((RectTransform)_message.transform, 0.5f, 0.68f, 950f, 120f);

            Button resurrect = UiFactory.ActionButton(root, "Resurrect", "RESURRECT  (AD)", UiFactory.Gold,
                () => _onResurrect?.Invoke());
            UiFactory.Place((RectTransform)resurrect.transform, 0.5f, 0.45f, 620f, 130f);
            _resurrectGo = resurrect.gameObject;

            Button giveUp = UiFactory.ActionButton(root, "GiveUp", "ACCEPT DEFEAT", UiFactory.InkSoft,
                () => _onGiveUp?.Invoke());
            UiFactory.Place((RectTransform)giveUp.transform, 0.5f, 0.3f, 560f, 110f);

            Hide();
        }

        public void Show(bool adAvailable, Action onResurrect, Action onGiveUp)
        {
            _onResurrect = onResurrect;
            _onGiveUp = onGiveUp;
            _resurrectGo.SetActive(adAvailable);
            _root.SetActive(true);
        }

        public void Hide() => _root.SetActive(false);
    }
}
