################## BEGIN PRELUDE #################

init python:
    import os
    import re
    
    class Log:
        def __init__(self):
            self.file = open(os.path.join(config.gamedir, 'custom_log.txt'), 'w')

        def info(self, s):
            self.file.write('{}\n'.format(s))
            self.file.flush()
    
    log = Log()

    # Used for stralias and numalias
    class VariableArray:
        def __init__(self, length, default_value):
            self.default_value = default_value
            self.values = [self.default_value] * length
            self.length = length

        def __getitem__(self, key):
            if key >= self.length:
                renpy.log("WARNING: attempt to get {} when size is {}".format(key, self.length))
                return self.default_value
            else:
                return self.values[key]

        def __setitem__(self, key, value):
            if key >= self.length:
                renpy.log("WARNING: attempt to set {} when size is {}".format(key, self.length))
            else:
                self.values[key] = value

    # Setup the (numeric) variable array (need to double check the actual variable count limit)
    pons_var = VariableArray(10000, 0)
    pons_str = VariableArray(10000, "")

    #def makeArrayRec(sizes, depth):
    #    if depth < len(sizes):
    #        return [makeArrayRec(sizes, depth+1) for _ in range(sizes[depth])]
    #    else:
    #        return 0
    #
    #def makeArray(*sizes):
    #    return makeArrayRec(sizes, 0)

    class MultiDimPhantom:
        def __init__(self, root_object, accessed_values):
            self.root_object = root_object
            self.accessed_values = accessed_values

        def __getitem__(self, key):
            self.accessed_values.append(key)

            if len(self.accessed_values) >= len(self.root_object.dimensions):
                return self.root_object.get(self.accessed_values)
            else:
                return MultiDimPhantom(self.root_object, self.accessed_values)

        def __setitem__(self, key, value):
            self.accessed_values.append(key)

            if len(self.accessed_values) == len(self.root_object.dimensions):
                return self.root_object.set(self.accessed_values, value)
            else:
                raise Exception("too few arguments for array set()")

    # Used for ponscripter arrays
    class Dim:
        def __init__(self, *dimensions):
            self.dimensions = dimensions
            accumulator = 1
            for dimension in dimensions:
                accumulator *= dimension
            self.arr = [0] * accumulator

        def get(self, indices):
            if self.dimension_check(indices):
                return self.arr[self.calculate_index(indices)]
            
            return 0

        def set(self, indices, value):
            if self.dimension_check(indices):
                self.arr[self.calculate_index(indices)] = value

        def calculate_index(self, indices):
            multiplier = 1
            index = 0
            for i in range(len(self.dimensions)):
                index += indices[i] * multiplier
                multiplier *= self.dimensions[i]

            return index

        def dimension_check(self, indices):
            if len(indices) != len(self.dimensions):
                raise Exception("not enough indices - got {} expected {}".format(indices, self.dimensions))

            # check each indice is within the corresponding dimension limit
            for i in range(len(self.dimensions)):
                if not (indices[i] < self.dimensions[i]):
                    renpy.log("Index [{}] (arg {}) was out of range - ind: {} dim: {}".format(indices[i], i, indices, self.dimensions))
                    return False
                    #raise Exception("Index [{}] (arg {}) was out of range - ind: {} dim: {}".format(indices[i], i, indices, self.dimensions))
            
            return True

        def __getitem__(self, firstKey):
            if len(self.dimensions) == 1:
                return self.get([firstKey])
            else:
                return MultiDimPhantom(self, [firstKey])

        # the only time this happens is if the value is directly set like a[3] = 5
        def __setitem__(self, singularKey, value):
            self.set([singularKey], value)

    class PonscripterButton:
        def __init__(self, id, x, y):
            self.text = "button"
            self.image_path = None #use later for button image?
            self.id = id;
            self.x = x;
            self.y = y;

################################ SPRITE ##################################
    def makeTransform(xpos, ypos, opacity):
        t = Transform()
        t.xpos = xpos
        t.ypos = ypos
        t.opacity = opacity
        return t

    class SpriteObject:
        tag_counter = 0

        def __init__(self, sprite_number, filename, x, y, opacity=None):
            self.sprite_number = sprite_number
            self.filename = filename
            self.x = x
            self.y = y
            self.opacity = opacity
            self.tag = str(SpriteObject.tag_counter)
            SpriteObject.tag_counter += 1

        def show(self):
            renpy.show(self.filename, at_list=[makeTransform(self.x, self.y, self.opacity)], tag=self.tag)

        def hide(self):
            renpy.hide(self.tag)

    class SpriteMap:
        def __init__(self):
            self.map = {}

        def set(self, sprite_object):
            self.map[sprite_object.sprite_number] = sprite_object

        def get(self, sprite_number):
            return self.map[sprite_number]

        def pop(self, sprite_number):
            sprite_object = self.map[sprite_object.sprite_number]
            self.map[sprite_number] = None
            return sprite_object

        def values(self):
            return self.map.values()

    # Expects the filename not to have any extension?
    def pons_lsp(sprite_number, filename_with_tags, x, y, opacity=None):
        splitFilename = filename.split(';')

        #TODO: use tags
        tags = None
        filename = filename_with_tags

        log.info("Loading file: '{}'".format(filename))

        if filename[0] == ':':
            semi_pos = filename.find(';')
            if semi_pos != -1:
                tags = filename_with_tags[1:semi_pos]
                filename = filename_with_tags[semi_pos+1:]

        if opacity is None:
            opacity = 100

        # Create sprite object
        sprite_object = SpriteObject(sprite_number, filename, x, y, opacity)

        #save sprite in the sprite array
        sprite_map.set(sprite_object)

    def pons_print():
        for sprite_object in sprite_map.values():
            sprite_object.show()
        renpy.with_statement(dissolve)

    def pons_clear(sprite_number):
        sprite_map.pop(sprite_number).hide()

################################ SPRITE ##################################
    class EffectMap:
        def __init__(self):
            self.map = {
                1: None,
                10: dissolve,
            }

        def set(self, effect_number, effect):
            self.map[effect_number] = effect
        
        def get(self, effect_number):
            if effect_number in self.map:
                return self.map[effect_number]
            else:
                renpy.log("Warning: Transition {} not implemented".format(effect_number))
                return None

    def makeTransform(xpos, ypos, opacity):
        t = Transform()
        t.xpos = xpos
        t.ypos = ypos
        t.opacity = opacity
        return t

    class SpriteObject:
        tag_counter = 0

        def __init__(self, sprite_number, filename_with_ext, x, y, opacity=None):
            self.sprite_number = sprite_number
            self.filename = os.path.splitext(filename_with_ext)[0]
            self.x = x
            self.y = y
            self.opacity = opacity
            self.tag = str(SpriteObject.tag_counter)
            SpriteObject.tag_counter += 1

        def show(self):
            renpy.show(self.filename, at_list=[makeTransform(self.x, self.y, self.opacity)], tag=self.tag)

        def hide(self):
            renpy.hide(self.tag)

    class SpriteMap:
        def __init__(self):
            self.map = {}

        def set(self, sprite_object):
            self.map[sprite_object.sprite_number] = sprite_object

        def get(self, sprite_number):
            return self.map[sprite_number]

        def pop(self, sprite_number):
            sprite_object = self.map[sprite_object.sprite_number]
            self.map[sprite_number] = None
            return sprite_object

        def values(self):
            return self.map.values()

    def pons_lsp(sprite_number, filename, x, y, opacity=None):
        if opacity is None:
            opacity = 100

        # Create sprite object
        sprite_object = SpriteObject(sprite_number, filename, x, y, opacity)

        #save sprite in the sprite array
        sprite_map.set(sprite_object)

    def pons_print(effect_number):
        for sprite_object in sprite_map.values():
            sprite_object.show()
        renpy.with_statement(effect_map.get(effect_number))

    def pons_clear(sprite_number):
        sprite_map.pop(sprite_number).hide()

    #TODO: properly use sprite as button graphic instead of sprite number
    def pons_spbtn(sprite_number, button_number):
        ponscripter_buttons[button_number] = PonscripterButton(button_number, 0, 0)

    def pons_btndef(filename):
        ponscripter_buttons = {}
        ponscripter_btndef_image = filename

################################ SPRITE ##################################

    # Holds the currently loaded sprtie/sprite numbers
    sprite_map = SpriteMap()
    # Holds the currently loaded effects (transitions)
    effect_map = EffectMap()

    # Renpy Config
    config.log = "ponscripty.log"

    # Global variable definitions
    ponscripter_buttons = {} #hashmap of button_id : PonscripterButton

    # global variable used for btndef command
    ponscripter_btndef_image = ""




screen MultiButton():
    vbox: #fixed: #Change to fixed when proper button x y implemented 
        for but_number, but in ponscripter_buttons.items():
            textbutton str(but.text):
                action Return(but.id)
                #xpos but.x #UNCOMMENT WHEN PROPER BUTTON X Y IPMLEMENTED
                #ypos but.y #UNCOMMENT WHEN PROPER BUTTON X Y IPMLEMENTED
                #left_padding i
                xfill True

# Declare characters used by this game.
define narrator = Character(_("Narrator"), color="#c8ffc8")

label start:
################### END PRELUDE ##################