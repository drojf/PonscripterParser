################## BEGIN PRELUDE #################

init python:
    class VariableArray:
        def __init__(self, length):
            self.values = [0] * length
            self.length = length

        def __getitem__(self, key):
            if key >= self.length:
                renpy.log("WARNING: attempt to get {} when size is {}".format(key, self.length))
                return 0
            else:
                return self.values[key]

        def __setitem__(self, key, value):
            if key >= self.length:
                renpy.log("WARNING: attempt to set {} when size is {}".format(key, self.length))
            else:
                self.values[key] = value

    # Setup the (numeric) variable array (need to double check the actual variable count limit)
    variable_array = VariableArray(10000)
    string_array = VariableArray(10000)

# Declare characters used by this game.
define narrator = Character(_("Narrator"), color="#c8ffc8")

label start:
################### END PRELUDE ##################