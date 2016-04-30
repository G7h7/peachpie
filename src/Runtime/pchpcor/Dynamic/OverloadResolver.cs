﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.Core.Dynamic
{
    /// <summary>
    /// Methods for selecting best method overload from possible candidates.
    /// </summary>
    internal static class OverloadResolver
    {
        /// <summary>
        /// Selects all method candidates.
        /// </summary>
        public static IEnumerable<MethodBase> SelectCandidates(this Type type)
        {
            return type.GetRuntimeMethods();
        }

        /// <summary>
        /// Selects only candidates of given name.
        /// </summary>
        public static IEnumerable<MethodBase> SelectByName(this IEnumerable<MethodBase> candidates, string name)
        {
            return candidates.Where(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Selects only candidates visible from the current class context.
        /// </summary>
        public static bool IsVisible(this MethodBase m, Type classCtx)
        {
            if (m.IsPrivate && m.DeclaringType != classCtx)
            {
                return false;
            }

            if (m.IsFamily)
            {
                if (classCtx == null)
                {
                    return false;
                }

                var m_type = m.DeclaringType.GetTypeInfo();
                var classCtx_type = classCtx.GetTypeInfo();

                if (!classCtx_type.IsAssignableFrom(m_type) && !m_type.IsAssignableFrom(classCtx_type))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Selects only static methods.
        /// </summary>
        public static IEnumerable<MethodBase> SelectStatic(this IEnumerable<MethodBase> candidates)
        {
            return candidates.Where(m => m.IsStatic);
        }

        /// <summary>
        /// Gets value indicating the parameter is a special context parameter.
        /// </summary>
        static bool IsContextParameter(ParameterInfo p)
        {
            return p.Position == 0 && p.ParameterType == typeof(Context);
        }

        /// <summary>
        /// Gets value indicating the parameter is a special late static bound parameter.
        /// </summary>
        static bool IsStaticBoundParameter(ParameterInfo p)
        {
            return p.ParameterType == typeof(Type) && p.Name == "<static>";
        }

        /// <summary>
        /// Gets value indicating the parameter is a special local parameters parameter.
        /// </summary>
        static bool IsLocalsParameter(ParameterInfo p)
        {
            return p.ParameterType == typeof(PhpArray) && p.Name == "<locals>";
        }

        /// <summary>
        /// Gets value indicating the parameter for variable parameters count.
        /// </summary>
        static bool IsParams(ParameterInfo[] parameters, ParameterInfo p)
        {
            return
                p.Position == parameters.Length - 1 &&
                p.ParameterType.IsArray &&
                p.GetCustomAttribute<ParamArrayAttribute>() != null;
        }

        /// <summary>
        /// Tries to bind arguments to method parameters.
        /// </summary>
        public static List<Expression> TryBindArguments(MethodBase m, DynamicMetaObject[] args, Expression ctx, Expression staticOpt = null, Expression localsOpt = null)
        {
            var result = new List<Expression>();

            var ps = m.GetParameters();
            int arg_index = 0;

            // TODO: restrictions
            // TODO: 'value' of the overload
            
            foreach (var p in ps)
            {
                if (arg_index == 0)
                {
                    // special parameters:

                    if (IsContextParameter(p))
                    {
                        result.Add(ctx);
                        continue;
                    }
                    if (IsStaticBoundParameter(p))
                    {
                        if (staticOpt == null) throw new ArgumentException("<static> missing.");
                        result.Add(staticOpt);
                        continue;
                    }
                    if (IsLocalsParameter(p))
                    {
                        if (localsOpt == null) throw new ArgumentException("<locals> missing.");
                        result.Add(localsOpt);
                        continue;
                    }
                }

                // params
                if (IsParams(ps, p))
                {
                    Debug.Assert(p.Position == ps.Length - 1);
                    Debug.Assert(p.ParameterType.HasElementType); // => Array

                    var exprs = new List<Expression>();
                    var ptype = p.ParameterType.GetElementType();

                    while (arg_index < args.Length)
                    {
                        exprs.Add(ConvertExpression.Bind(args[arg_index++].Expression, ptype));
                    }

                    result.Add(Expression.NewArrayInit(ptype, exprs.ToArray()));
                }
                else
                {
                    // regular parameters
                    if (arg_index < args.Length)
                    {
                        result.Add(ConvertExpression.Bind(args[arg_index++].Expression, p.ParameterType));
                    }
                    else
                    {
                        if (p.IsOptional)
                        {
                            Debug.Assert(p.HasDefaultValue);
                            result.Add(Expression.Constant(p.DefaultValue, p.ParameterType));
                        }
                        else
                        {
                            throw new ArgumentException("mandatory parameter not provided");
                        }
                    }
                }
            }

            //
            return result;
        }

        public static IEnumerable<MethodBase> SelectWithArguments(this IEnumerable<MethodBase> candidates, Expression[] arguments)
        {
            //return candidates.Where(m => CanBeCalled(m, arguments));
            throw new NotImplementedException();
        }
    }
}
