using System.Collections.Generic;
using System.Linq;
using OpenDDD.Application;
using OpenDDD.Application.Error;
using OpenDDD.Domain.Model.Validation;

namespace Application.Actions.Commands
{
    public class PredictWeatherCommand : Command
    {
        

        public override void Validate()
        {
            var errors = GetErrors();

            if (errors.Any())
                throw new InvalidCommandException(this, errors);
        }

        public override IEnumerable<ValidationError> GetErrors()
        {
            var errors = new Validator<PredictWeatherCommand>(this)
                .Errors();

            return errors;
        }
    }
}
