﻿using ACMESharp;
using LetsEncrypt.ACME.Simple.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsEncrypt.ACME.Simple.Plugins.ValidationPlugins
{
    public interface IValidationPlugin : IHasName
    {
        /// <summary>
        /// Type of challange
        /// </summary>
        string ChallengeType { get; }

        /// <summary>
        /// Prepare challange
        /// </summary>
        /// <param name="options"></param>
        /// <param name="target"></param>
        /// <param name="challenge"></param>
        /// <returns></returns>
        Action<AuthorizationState> PrepareChallenge(Target target, AuthorizeChallenge challenge, string identifier, Options options, InputService input);

        /// <summary>
        /// Is this plugin capable of validating the target
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        bool CanValidate(Target target);

        /// <summary>
        /// Prepare the plugin instance for validating the target (e.g. apply settings)
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        IValidationPlugin CreateInstance(Target target);

        /// <summary>
        /// Check or get information need for validation (interactive)
        /// </summary>
        /// <param name="target"></param>
        void Aquire(OptionsService options, InputService input, Target target);

        /// <summary>
        /// Check information need for validation (unattended)
        /// </summary>
        /// <param name="target"></param>
        void Default(OptionsService options, Target target);
    }
}
