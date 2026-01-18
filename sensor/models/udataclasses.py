__version__ = '2025.1'
__author__ = 'mirek <miroslav.binas@tuke.sk>'
__all__ = [
    'validator',
    'Dataclass'
]

#udataclasses.py


def validator(field):
    def decorator(func):
        class CallableWrapper:
            def __init__(self, f):
                self._func = f
                self._field = field
                self._is_validator = True
            def __call__(self, instance, value):
                return self._func(instance, value)
        return CallableWrapper(func)
    return decorator


class Dataclass:
    def __init__(self, **kwargs):
        setattr(self.__class__, '__validators', {})
        setattr(self.__class__, '__annotations__', {})

        for key, value in self.__class__.__dict__.items():
            # convert class attributes to fields
            if not key.startswith("__") and not callable(value):
                setattr(self, key, value)

            # get validators
            if callable(value) is True and getattr(value, "_is_validator", False) is True:
                field = value._field
                self.__class__.__validators.setdefault(field, []).append(value)

        # update fields with kwargs
        for field, value in kwargs.items():
            setattr(self, field, value)

        # create field annotations
        for field, value in self.__dict__.items():
            self.__class__.__annotations__[field] = type(value)

    def __iter__(self):
        return iter(self.__dict__.items())

    def __setattr__(self, name, value):
        # if attribute doesnt exist, raise exception (this is slotted class by default)
        if not hasattr(self, name):
            raise AttributeError(f'Attribute "{name}" is not in class {self.__class__.__name__}.')

        # check data type before assignment
        current_value = getattr(self, name, None)
        current_type = type(current_value)

        # first assignment is OK if field is None
        if current_value is None:
            super().__setattr__(name, value)

        # if value is dictionary, then expect it's key is of custom class type
        elif isinstance(value, dict):
            super().__setattr__(name, current_type(**value))

        # if value is not of default attr type, then raise exception
        elif not isinstance(value, current_type):
            raise ValueError(f'Value "{value}" for attribute "{name}" is not of type "{current_type.__name__}".')

        else:
            super().__setattr__(name, value)

        # custom validators
        validators = getattr(self.__class__, "__validators", {})
        if name in validators:
            for vfunc in validators[name]:
                vfunc(self, value)

    def __repr__(self) -> str:
        items = [f"{key}={repr(value)}" for key, value in self.__dict__.items()]
        return f"{self.__class__.__name__}({','.join(items)})"

    def model_dump(self) -> dict:
        result = {}

        for field, value in self.__dict__.items():
            if isinstance(value, Dataclass):
                result[field] = value.model_dump()
            else:
                result[field] = value

        return result
